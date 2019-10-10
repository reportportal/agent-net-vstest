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
using ReportPortal.Shared.Internal.Logging;

namespace ReportPortal.VSTest.TestLogger
{
    [ExtensionUri("logger://ReportPortal")]
    [FriendlyName("ReportPortal")]
    public class ReportPortalLogger : ITestLoggerWithParameters
    {
        private ITraceLogger TraceLogger { get; } = TraceLogManager.GetLogger(typeof(ReportPortalLogger));

        private IConfigurationBuilder _configBuilder;
        private IConfiguration _config;

        private readonly Dictionary<TestOutcome, Status> _statusMap = new Dictionary<TestOutcome, Status>();

        private ILaunchReporter _launchReporter;

        // key: namespace
        private Dictionary<string, ITestReporter> _suitesflow = new Dictionary<string, ITestReporter>();

        public ReportPortalLogger()
        {
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
            var launchId = _config.GetValue("Launch:Id", "");

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
            try
            {
                var innerResultsCountProperty = e.Result.Properties.FirstOrDefault(p => p.Id == "InnerResultsCount");
                if (innerResultsCountProperty == null || (innerResultsCountProperty != null && (int)e.Result.GetPropertyValue(innerResultsCountProperty) == 0))
                {
                    string className;
                    string testName;
                    var fullName = e.Result.TestCase.FullyQualifiedName;
                    if (e.Result.TestCase.ExecutorUri.Host == "xunit")
                    {
                        var testMethodName = fullName.Split('.').Last();
                        var testClassName = fullName.Substring(0, fullName.LastIndexOf('.'));
                        var displayName = e.Result.TestCase.DisplayName;

                        testName = displayName == fullName
                            ? testMethodName
                            : displayName.Replace($"{testClassName}.", string.Empty);

                        className = testClassName;
                    }
                    else if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("mstest"))
                    {
                        testName = e.Result.DisplayName ?? e.Result.TestCase.DisplayName;

                        var classNameProperty = e.Result.TestCase.Properties.FirstOrDefault(p => p.Id == "MSTestDiscoverer.TestClassName");
                        if (classNameProperty != null)
                        {
                            className = e.Result.TestCase.GetPropertyValue(classNameProperty).ToString();
                        }
                        // else get classname from FQN (mstestadapter/v1)
                        else
                        {
                            // and temporary consider testname from TestCase object instead of from Result object
                            // Result.DisplayName: Test1 (Data Row 0)
                            // TestCase.DisplayName Test1
                            // the first name is better in report, but consider the second name to identify 'className'
                            testName = e.Result.TestCase.DisplayName ?? e.Result.DisplayName;
                            className = fullName.Substring(0, fullName.Length - testName.Length - 1);
                            testName = e.Result.DisplayName ?? e.Result.TestCase.DisplayName;
                        }
                    }
                    else
                    {
                        testName = e.Result.TestCase.DisplayName ?? fullName.Split('.').Last();

                        className = fullName.Substring(0, fullName.Length - testName.Length - 1);
                    }

                    TraceLogger.Info($"ClassName: {className}, TestName: {testName}");

                    var rootNamespaces = _config.GetValues<string>("rootNamespaces", null);
                    if (rootNamespaces != null)
                    {
                        var rootNamespace = rootNamespaces.FirstOrDefault(rns => className.StartsWith(rns));
                        if (rootNamespace != null)
                        {
                            className = className.Substring(rootNamespace.Length + 1);
                            TraceLogger.Verbose($"Cutting '{rootNamespace}'... New ClassName is '{className}'.");
                        }
                    }

                    var suiteReporter = GetOrStartSuiteNode(className, e.Result.StartTime.UtcDateTime);

                    // find description
                    var testDescription = e.Result.TestCase.Traits.FirstOrDefault(x => x.Name == "Description")?.Value;

                    if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("mstestadapter"))
                    {
                        var testProperty = e.Result.TestCase.Properties.FirstOrDefault(p => p.Id == "Description");
                        if (testProperty != null)
                        {
                            testDescription = e.Result.TestCase.GetPropertyValue(testProperty).ToString();
                        }
                    }

                    // find categories
                    var testCategories = e.Result.TestCase.Traits.Where(t => t.Name.ToLower() == "Category".ToLower()).Select(x => x.Value).ToList();

                    if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("mstestadapter"))
                    {
                        var testProperty = e.Result.TestCase.Properties.FirstOrDefault(p => p.Id == "MSTestDiscoverer.TestCategory");
                        if (testProperty != null)
                        {
                            testCategories.AddRange((string[])e.Result.TestCase.GetPropertyValue(testProperty));
                        }
                    }
                    else if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("nunit"))
                    {
                        var testProperty = e.Result.TestCase.Properties.FirstOrDefault(p => p.Id == "NUnit.TestCategory");
                        if (testProperty != null)
                        {
                            testCategories.AddRange((string[])e.Result.TestCase.GetPropertyValue(testProperty));
                        }
                    }

                    // start test node
                    var startTestRequest = new StartTestItemRequest
                    {
                        Name = testName,
                        Description = testDescription,
                        Tags = testCategories,
                        StartTime = e.Result.StartTime.UtcDateTime,
                        Type = TestItemType.Step
                    };

                    var testReporter = suiteReporter.StartChildTestReporter(startTestRequest);

                    // add log messages
                    if (e.Result.Messages != null)
                    {
                        foreach (var message in e.Result.Messages)
                        {
                            if (message.Text == null) continue;
                            foreach (var line in message.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var handled = false;

                                try
                                {
                                    SharedLogMessage sharedMessage;

                                    // SpecRun adapter add this for output messages, just trim it for internal messages
                                    if (line.StartsWith("-> "))
                                    {
                                        sharedMessage = Client.Converters.ModelSerializer.Deserialize<SharedLogMessage>(line.Substring(3));
                                    }
                                    else
                                    {
                                        sharedMessage = Client.Converters.ModelSerializer.Deserialize<SharedLogMessage>(line);
                                    }

                                    var logRequest = new AddLogItemRequest
                                    {
                                        Level = sharedMessage.Level,
                                        Time = sharedMessage.Time,
                                        TestItemId = sharedMessage.TestItemId,
                                        Text = sharedMessage.Text
                                    };
                                    if (sharedMessage.Attach != null)
                                    {
                                        logRequest.Attach = new Attach
                                        {
                                            Name = sharedMessage.Attach.Name,
                                            MimeType = sharedMessage.Attach.MimeType,
                                            Data = sharedMessage.Attach.Data
                                        };
                                    }

                                    testReporter.Log(logRequest);

                                    handled = true;
                                }
                                catch (Exception)
                                {

                                }

                                if (!handled)
                                {
                                    testReporter.Log(new AddLogItemRequest
                                    {
                                        Time = DateTime.UtcNow,
                                        Level = LogLevel.Info,
                                        Text = line
                                    });
                                }
                            }
                        }
                    }

                    if (e.Result.ErrorMessage != null)
                    {
                        testReporter.Log(new AddLogItemRequest
                        {
                            Time = e.Result.EndTime.UtcDateTime,
                            Level = LogLevel.Error,
                            Text = e.Result.ErrorMessage + "\n" + e.Result.ErrorStackTrace
                        });
                    }

                    // add attachments
                    if (e.Result.Attachments != null)
                    {
                        foreach (var attachmentSet in e.Result.Attachments)
                        {
                            foreach (var attachmentData in attachmentSet.Attachments)
                            {
                                var filePath = attachmentData.Uri.AbsolutePath;

                                var attachmentLogRequest = new AddLogItemRequest
                                {
                                    Level = LogLevel.Info,
                                    Text = Path.GetFileName(filePath),
                                    Time = e.Result.EndTime.UtcDateTime
                                };

                                try
                                {
                                    var fileExtension = Path.GetExtension(filePath);

                                    attachmentLogRequest.Attach = new Attach(Path.GetFileName(filePath), Shared.MimeTypes.MimeTypeMap.GetMimeType(fileExtension), File.ReadAllBytes(filePath));
                                }
                                catch (Exception exp)
                                {
                                    attachmentLogRequest.Level = LogLevel.Warning;
                                    attachmentLogRequest.Text = $"Cannot read a content of '{filePath}' file: {exp.Message}";
                                }

                                testReporter.Log(attachmentLogRequest);
                            }
                        }
                    }

                    // finish test

                    var finishTestRequest = new FinishTestItemRequest
                    {
                        EndTime = e.Result.EndTime.UtcDateTime,
                        Status = _statusMap[e.Result.Outcome]
                    };

                    testReporter.Finish(finishTestRequest);
                }
            }
            catch(Exception exp)
            {
                Console.WriteLine($"ReportPortal unexpected exception in parsing test result: {exp}");
            }
        }

        private void Events_TestRunComplete(object sender, TestRunCompleteEventArgs e)
        {
            //TODO: apply smarter way to finish suites in real-time tests execution
            //finish suites

            while (_suitesflow.Count != 0)
            {
                var deeperKey = _suitesflow.Keys.OrderBy(s => s.Split('.').Length).Last();

                TraceLogger.Verbose($"Finishing namespace '{deeperKey}'");
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

        private ITestReporter GetOrStartSuiteNode(string fullName, DateTime startTime)
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
                            StartTime = startTime,
                            Type = TestItemType.Suite
                        };

                        var rootSuite = _launchReporter.StartChildTestReporter(startSuiteRequest);

                        _suitesflow[fullName] = rootSuite;
                        return rootSuite;
                    }
                }
                else
                {
                    var parent = GetOrStartSuiteNode(string.Join(".", parts.Take(parts.Length - 1)), startTime);

                    // create
                    var startSuiteRequest = new StartTestItemRequest
                    {
                        Name = parts.Last(),
                        StartTime = startTime,
                        Type = TestItemType.Suite
                    };

                    var suite = parent.StartChildTestReporter(startSuiteRequest);

                    _suitesflow[fullName] = suite;

                    return suite;
                }
            }
        }
    }
}
