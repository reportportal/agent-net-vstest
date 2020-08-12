using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Collections.Generic;
using ReportPortal.Client;
using System.Diagnostics;
using System.Linq;
using System.IO;
using ReportPortal.Shared.Configuration;
using ReportPortal.Shared.Reporter;
using ReportPortal.VSTest.TestLogger.Configuration;
using ReportPortal.Shared.Internal.Logging;
using ReportPortal.Client.Abstractions.Requests;
using ReportPortal.Client.Abstractions.Models;
using ReportPortal.VSTest.TestLogger.LogHandler.Messages;

namespace ReportPortal.VSTest.TestLogger
{
    [ExtensionUri("logger://ReportPortal")]
    [FriendlyName("ReportPortal")]
    public class ReportPortalLogger : ITestLoggerWithParameters
    {
        private ITraceLogger TraceLogger { get; }

        private IConfigurationBuilder _configBuilder;
        private IConfiguration _config;

        private readonly Dictionary<TestOutcome, Status> _statusMap = new Dictionary<TestOutcome, Status>();

        private ILaunchReporter _launchReporter;

        // key: namespace
        private Dictionary<string, ITestReporter> _suitesflow = new Dictionary<string, ITestReporter>();

        public ReportPortalLogger()
        {
            var testLoggerDirectory = Path.GetDirectoryName(new Uri(typeof(ReportPortalLogger).Assembly.CodeBase).LocalPath);

            TraceLogger = TraceLogManager.Instance.WithBaseDir(testLoggerDirectory).GetLogger(typeof(ReportPortalLogger));

            TraceLogger.Verbose($"This test logger base directory: {testLoggerDirectory}");

            // Seems Visual Studio Test Host  for net core uses built-in vstestconsole for netcoreapp1.0
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            var jsonPath = Path.Combine(testLoggerDirectory, "ReportPortal.config.json");
            _configBuilder = new ConfigurationBuilder().AddJsonFile(jsonPath).AddEnvironmentVariables();

            _statusMap[TestOutcome.None] = Status.Skipped;
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
            try
            {
                _config = _configBuilder.Build();

                if (_config.GetValue("Enabled", true))
                {
                    events.TestRunStart += Events_TestRunStart;
                    events.TestResult += Events_TestResult;
                    events.TestRunComplete += Events_TestRunComplete;

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
                }
            }
            catch (Exception exp)
            {
                var error = $"Unexpected exception in {nameof(Initialize)}: {exp}";
                TraceLogger.Error(error);
                Console.WriteLine(error);
                throw;
            }
        }

        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="parameters">Configuration parameters for logger.</param>
        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            try
            {
                _configBuilder.Add(new LoggerConfigurationProvider(parameters));

                Initialize(events, parameters.Single(p => p.Key == "TestRunDirectory").Value);
            }
            catch (Exception exp)
            {
                var error = $"Unexpected exception in {nameof(Initialize)}: {exp}";
                TraceLogger.Error(error);
                Console.WriteLine(error);
                throw;
            }
        }

        private void Events_TestRunStart(object sender, TestRunStartEventArgs e)
        {
            try
            {
                var apiUri = _config.GetValue<string>(ConfigurationPath.ServerUrl);
                var apiProject = _config.GetValue<string>(ConfigurationPath.ServerProject);
                var apiToken = _config.GetValue<string>(ConfigurationPath.ServerAuthenticationUuid);
                var apiService = new Service(new Uri(apiUri), apiProject, apiToken);

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

                requestNewLaunch.Attributes = _config.GetKeyValues("Launch:Attributes", new List<KeyValuePair<string, string>>()).Select(a => new ItemAttribute { Key = a.Key, Value = a.Value }).ToList();

                Shared.Extensibility.Analytics.AnalyticsReportEventsObserver.DefineConsumer("agent-dotnet-vstest");

                _launchReporter = new LaunchReporter(apiService, _config, null, Shared.Extensibility.ExtensionManager.Instance);

                _launchReporter.Start(requestNewLaunch);
            }
            catch (Exception exp)
            {
                var error = $"Unexpected exception in {nameof(Events_TestRunStart)}: {exp}";
                TraceLogger.Error(error);
                Console.WriteLine(error);
                throw;
            }
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

                    if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("mstest"))
                    {
                        var testProperty = e.Result.TestCase.Properties.FirstOrDefault(p => p.Id == "Description");
                        if (testProperty != null)
                        {
                            testDescription = e.Result.TestCase.GetPropertyValue(testProperty).ToString();
                        }
                    }

                    // find categories
                    var testCategories = e.Result.TestCase.Traits.Where(t => t.Name.ToLower() == "Category".ToLower()).Select(x => x.Value).ToList();

                    if (e.Result.TestCase.ExecutorUri.ToString().ToLower().Contains("mstest"))
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
                        Attributes = testCategories.Select(tc => new ItemAttribute { Key = "Category", Value = tc }).ToList(),
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

                                var textMessage = line;

                                try
                                {
                                    // SpecRun adapter adds this for output messages, just trim it for internal text messages
                                    if (line.StartsWith("-> "))
                                    {
                                        textMessage = line.Substring(3);
                                    }

                                    var baseCommunicationMessage = Client.Converters.ModelSerializer.Deserialize<BaseCommunicationMessage>(textMessage);

                                    switch (baseCommunicationMessage.Action)
                                    {
                                        case CommunicationAction.AddLog:
                                            var addLogCommunicationMessage = Client.Converters.ModelSerializer.Deserialize<AddLogCommunicationMessage>(textMessage);
                                            handled = HandleAddLogCommunicationAction(testReporter, addLogCommunicationMessage);
                                            break;
                                        case CommunicationAction.BeginLogScope:
                                            var beginLogScopeCommunicationMessage = Client.Converters.ModelSerializer.Deserialize<BeginScopeCommunicationMessage>(textMessage);
                                            handled = HandleBeginLogScopeCommunicationAction(testReporter, beginLogScopeCommunicationMessage);
                                            break;
                                        case CommunicationAction.EndLogScope:
                                            var endLogScopeCommunicationMessage = Client.Converters.ModelSerializer.Deserialize<EndScopeCommunicationMessage>(textMessage);
                                            handled = HandleEndLogScopeCommunicationMessage(endLogScopeCommunicationMessage);
                                            break;
                                    }
                                }
                                catch (Exception)
                                {

                                }

                                if (!handled)
                                {
                                    // consider line output as usual user's log message

                                    testReporter.Log(new CreateLogItemRequest
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
                        testReporter.Log(new CreateLogItemRequest
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
                                var filePath = attachmentData.Uri.LocalPath;

                                try
                                {
                                    var attachmentLogRequest = new CreateLogItemRequest
                                    {
                                        Level = LogLevel.Info,
                                        Text = Path.GetFileName(filePath),
                                        Time = e.Result.EndTime.UtcDateTime
                                    };

                                    var fileExtension = Path.GetExtension(filePath);

                                    byte[] bytes;

                                    using (var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                                    {
                                        using (var memoryStream = new MemoryStream())
                                        {
                                            fileStream.CopyTo(memoryStream);
                                            bytes = memoryStream.ToArray();
                                        }
                                    }

                                    attachmentLogRequest.Attach = new LogItemAttach(Shared.MimeTypes.MimeTypeMap.GetMimeType(fileExtension), bytes);

                                    testReporter.Log(attachmentLogRequest);
                                }
                                catch (Exception exp)
                                {
                                    var error = $"Cannot read a content of '{filePath}' file: {exp.Message}";

                                    testReporter.Log(new CreateLogItemRequest
                                    {
                                        Level = LogLevel.Warning,
                                        Time = e.Result.EndTime.UtcDateTime,
                                        Text = error
                                    });

                                    TraceLogger.Error(error);
                                }
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
            catch (Exception exp)
            {
                var error = $"Unexpected exception in {nameof(Events_TestResult)}: {exp}";
                TraceLogger.Error(error);
                Console.WriteLine(error);
            }
        }

        private void Events_TestRunComplete(object sender, TestRunCompleteEventArgs e)
        {
            try
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
                    _launchReporter.Sync();
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp);
                    throw;
                }

                stopwatch.Stop();
                Console.WriteLine($" Sync time: {stopwatch.Elapsed}");
            }
            catch (Exception exp)
            {
                var error = $"Unexpected exception in {nameof(Events_TestRunComplete)}: {exp}";
                TraceLogger.Error(error);
                Console.WriteLine(error);
            }
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

        // key: log scope ID, value: according TestReporter
        private Dictionary<string, ITestReporter> _nestedSteps = new Dictionary<string, ITestReporter>();

        private bool HandleAddLogCommunicationAction(ITestReporter testReporter, AddLogCommunicationMessage message)
        {
            var logRequest = new CreateLogItemRequest
            {
                Level = message.Level,
                Time = message.Time,
                Text = message.Text
            };

            if (message.Attach != null)
            {
                logRequest.Attach = new LogItemAttach
                {
                    MimeType = message.Attach.MimeType,
                    Data = message.Attach.Data
                };
            }

            if (message.ParentScopeId != null)
            {
                testReporter = _nestedSteps[message.ParentScopeId];
            }

            testReporter.Log(logRequest);

            return true;
        }

        private bool HandleBeginLogScopeCommunicationAction(ITestReporter testReporter, BeginScopeCommunicationMessage message)
        {
            var startTestItemRequest = new StartTestItemRequest
            {
                Name = message.Name,
                StartTime = message.BeginTime,
                Type = TestItemType.Step,
                HasStats = false
            };

            if (message.ParentScopeId != null)
            {
                testReporter = _nestedSteps[message.ParentScopeId];
            }

            var nestedStep = testReporter.StartChildTestReporter(startTestItemRequest);

            _nestedSteps[message.Id] = nestedStep;

            return true;
        }

        private Dictionary<Shared.Execution.Logging.LogScopeStatus, Status> _nestedStepStatusMap = new Dictionary<Shared.Execution.Logging.LogScopeStatus, Status> {
            { Shared.Execution.Logging.LogScopeStatus.InProgress, Status.InProgress },
            { Shared.Execution.Logging.LogScopeStatus.Passed, Status.Passed },
            { Shared.Execution.Logging.LogScopeStatus.Failed, Status.Failed },
            { Shared.Execution.Logging.LogScopeStatus.Skipped,Status.Skipped }
        };

        private bool HandleEndLogScopeCommunicationMessage(EndScopeCommunicationMessage message)
        {
            var nestedStep = _nestedSteps[message.Id];

            nestedStep.Finish(new FinishTestItemRequest
            {
                EndTime = message.EndTime,
                Status = _nestedStepStatusMap[message.Status]
            });

            _nestedSteps.Remove(message.Id);

            return true;
        }
    }
}
