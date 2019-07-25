using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Collections.Generic;
using ReportPortal.Client.Models;
using ReportPortal.Client;
using ReportPortal.Shared;
using ReportPortal.Client.Requests;
using System.Diagnostics;
using System.Linq;
using System.IO;
using ReportPortal.Shared.Configuration;
using ReportPortal.Shared.Reporter;
using ReportPortal.Shared.Configuration.Providers;
using ReportPortal.VSTest.TestLogger.Configuration;
using ReportPortal.VSTest.TestLogger.Helpers;

namespace ReportPortal.VSTest.TestLogger
{
    [ExtensionUri("logger://ReportPortal")]
    [FriendlyName("ReportPortal")]
    public class ReportPortalLogger : ITestLoggerWithParameters
    {
        private IConfigurationBuilder _configBuilder;
        private IConfiguration _config;
        private LogRequestItemGenerator _logRequestItemGenerator;

        private readonly Dictionary<TestOutcome, Status> _statusMap = new Dictionary<TestOutcome, Status>();

        private ILaunchReporter _launchReporter;

        // key: namespace
        private Dictionary<string, ITestReporter> _suitesflow = new Dictionary<string, ITestReporter>();

        public ReportPortalLogger()
        {
            _logRequestItemGenerator = new LogRequestItemGenerator();
            var jsonPath = Path.GetDirectoryName(new Uri(typeof(ReportPortalLogger).Assembly.CodeBase).LocalPath) + "/ReportPortal.config.json";
            _configBuilder = new ConfigurationBuilder().AddJsonFile(jsonPath).AddEnvironmentVariables();

            _statusMap[TestOutcome.Passed] = Status.Passed;
            _statusMap[TestOutcome.Failed] = Status.Failed;
            _statusMap[TestOutcome.Skipped] = Status.Skipped;
            _statusMap[TestOutcome.NotFound] = Status.Skipped;
        }

        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="testRunDirectory">Test Run Directory</param>
        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            _config = _configBuilder.Build();

            if (_config.GetValue("Enabled", true))
            {
                events.TestRunStart += Events_TestRunStart;
                events.TestResult += Events_TestResult;
                events.TestRunComplete += Events_TestRunComplete;


                var uri = _config.GetValue<string>(ConfigurationPath.ServerUrl);
                var project = _config.GetValue<string>(ConfigurationPath.ServerProject);
                var password = _config.GetValue<string>(ConfigurationPath.ServerAuthenticationUuid);

                //IWebProxy proxy = null;
                //if (Configuration.ReportPortal.Server.Proxy.ElementInformation.IsPresent)
                //{

                //    proxy = new WebProxy(Configuration.ReportPortal.Server.Proxy.Server);
                //    if (!String.IsNullOrEmpty(Configuration.ReportPortal.Server.Proxy.Username) && !String.IsNullOrEmpty(Configuration.ReportPortal.Server.Proxy.Password))
                //    {
                //        proxy.Credentials = String.IsNullOrEmpty(Configuration.ReportPortal.Server.Proxy.Domain)==false
                //            ? new NetworkCredential(Configuration.ReportPortal.Server.Proxy.Username, Configuration.ReportPortal.Server.Proxy.Password, Configuration.ReportPortal.Server.Proxy.Domain)
                //            : new NetworkCredential(Configuration.ReportPortal.Server.Proxy.Username, Configuration.ReportPortal.Server.Proxy.Password);
                //    }
                //}

                Bridge.Service = new Service(new Uri(uri), project, password);
            }
        }

        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="parameters">Configuration parameters for logger.</param>
        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            _configBuilder.Add(new LoggerConfigurationProvider(parameters));

            Initialize(events, parameters.Single(p => p.Key == "TestRunDirectory").Value);
        }

        private void Events_TestRunStart(object sender, TestRunStartEventArgs e)
        {
            var requestNewLaunch = new StartLaunchRequest
            {
                Name = _config.GetValue(ConfigurationPath.LaunchName, "VsTest Launch"),
                Description = _config.GetValue(ConfigurationPath.LaunchDescription, ""),
                StartTime = DateTime.UtcNow
            };
            if (_config.GetValue(ConfigurationPath.LaunchDebugMode, false))
            {
                requestNewLaunch.Mode = LaunchMode.Debug;
            }

            requestNewLaunch.Tags = _config.GetValues(ConfigurationPath.LaunchTags, new List<string>()).ToList();

            // see wether we need use external launch
            var launchId = _config.GetValue<string>("Launch:Id", "");

            if (string.IsNullOrEmpty(launchId))
            {
                _launchReporter = new LaunchReporter(Bridge.Service);
            }
            else
            {
                _launchReporter = new LaunchReporter(Bridge.Service, launchId);
            }

            _launchReporter.Start(requestNewLaunch);
        }

        private void Events_TestResult(object sender, TestResultEventArgs e)
        {
            var fullName = e.Result.TestCase.FullyQualifiedName;
            var testName = e.Result.TestCase.DisplayName ?? fullName.Split('.').Last();

            var fullPath = fullName.Substring(0, fullName.Length - testName.Length - 1);

            var rootNamespaces = _config.GetValues<string>("rootNamespaces", null);
            if (rootNamespaces != null)
            {
                var rootNamespace = rootNamespaces.FirstOrDefault(rns => fullPath.StartsWith(rns));
                if (rootNamespace != null)
                {
                    fullPath = fullPath.Substring(rootNamespace.Length + 1);
                }
            }

            var suiteReporter = GetOrStartSuiteNode(fullPath, e);

            // start test node
            var description = e.Result.TestCase.Traits.FirstOrDefault(x => x.Name == "Description");
            var startTestRequest = new StartTestItemRequest
            {
                Name = testName,
                Description = description?.Value,
                Tags = e.Result.TestCase.Traits.Where(t => t.Name.ToLower() == "Category".ToLower()).Select(x => x.Value).ToList(),
                StartTime = e.Result.StartTime.UtcDateTime,
                Type = TestItemType.Step
            };

            var testReporter = suiteReporter.StartChildTestReporter(startTestRequest);

            // add log messages
            SendLogs(testReporter, e.Result);

            // finish test

            var finishTestRequest = new FinishTestItemRequest
            {
                EndTime = e.Result.EndTime.UtcDateTime,
                Status = _statusMap[e.Result.Outcome]
            };

            testReporter.Finish(finishTestRequest);
        }

        private void Events_TestRunComplete(object sender, TestRunCompleteEventArgs e)
        {
            //TODO: apply smarter way to finish suites in real-time tests execution
            //finish suites
            while (_suitesflow.Count != 0)
            {
                var deeperKey = _suitesflow.Keys.OrderBy(s => s.Split('.').Length).Last();

                var deeperSuite = _suitesflow[deeperKey];

                var finishSuiteRequest = new FinishTestItemRequest
                {
                    EndTime = DateTime.UtcNow,
                    //TODO: identify correct suite status based on inner nodes
                    Status = Status.Passed
                };

                deeperSuite.Finish(finishSuiteRequest);
                _suitesflow.Remove(deeperKey);
            }

            // finish launch
            var requestFinishLaunch = new FinishLaunchRequest
            {
                EndTime = DateTime.UtcNow
            };

            _launchReporter.Finish(requestFinishLaunch);

            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.Write("Finishing to send results to Report Portal...");

            try
            {
                _launchReporter.FinishTask.Wait(TimeSpan.FromMinutes(30));
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp);
                throw;
            }

            stopwatch.Stop();
            Console.WriteLine($" Sync time: {stopwatch.Elapsed}");
        }

        private ITestReporter GetOrStartSuiteNode(string fullName, TestResultEventArgs e)
        {
            if (_suitesflow.ContainsKey(fullName))
            {
                return _suitesflow[fullName];
            }
            else
            {
                var parts = fullName.Split('.');

                if (parts.Length == 1)
                {
                    if (_suitesflow.ContainsKey(parts[0]))
                    {
                        return _suitesflow[parts[0]];
                    }
                    else
                    {
                        // create root
                        var startSuiteRequest = new StartTestItemRequest
                        {
                            Name = fullName,
                            StartTime = e.Result.StartTime.UtcDateTime,
                            Type = TestItemType.Suite
                        };

                        var rootSuite = _launchReporter.StartChildTestReporter(startSuiteRequest);

                        _suitesflow[fullName] = rootSuite;
                        return rootSuite;
                    }
                }
                else
                {
                    var parent = GetOrStartSuiteNode(string.Join(".", parts.Take(parts.Length - 1)), e);

                    // create
                    var startSuiteRequest = new StartTestItemRequest
                    {
                        Name = parts.Last(),
                        StartTime = e.Result.StartTime.UtcDateTime,
                        Type = TestItemType.Suite
                    };

                    var suite = parent.StartChildTestReporter(startSuiteRequest);

                    _suitesflow[fullName] = suite;

                    return suite;
                }
            }
        }

        private void SendLogs(ITestReporter testReporter, TestResult results)
        {
            if (results.Messages != null)
            {
                var messages = _logRequestItemGenerator.BuildLogItemRequest(results.Messages);
                foreach (var message in messages)
                {
                    testReporter.Log(message);
                }
            }

            if (results.ErrorMessage != null)
            {
                testReporter.Log(new AddLogItemRequest
                {
                    Time = results.EndTime.UtcDateTime,
                    Level = LogLevel.Error,
                    Text = $"{results.ErrorMessage }\n{results.ErrorStackTrace}"
                });
            }

            if (results.Attachments != null)
            {
                var attachmentsLogItemRequests = _logRequestItemGenerator.GetAttachmentRequest(results.Attachments, results.EndTime.UtcDateTime);

                foreach (var attachmentsRequest in attachmentsLogItemRequests)
                {
                    testReporter.Log(attachmentsRequest);
                }
            }
        }
    }
}
