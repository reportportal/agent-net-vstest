using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Net;
using System.Collections.Generic;
using ReportPortal.Client.Models;
using ReportPortal.Client;
using ReportPortal.Shared;
using ReportPortal.Client.Requests;
using System.Diagnostics;
using System.Linq;
using System.IO;
using ReportPortal.VSTest.TestLogger.Configuration;

namespace ReportPortal.VSTest.TestLogger
{
    [ExtensionUri("logger://ReportPortal")]
    [FriendlyName("ReportPortal")]
    public class ReportPortalLogger : ITestLogger
    {
        private Config Config;

        private readonly Dictionary<TestOutcome, Status> _statusMap = new Dictionary<TestOutcome, Status>();

        private bool _isRunStarted = false;
        private TestResult _lastTestResult;

        public ReportPortalLogger()
        {
            var configPath = Path.GetDirectoryName(new Uri(typeof(Config).Assembly.CodeBase).LocalPath) + "/ReportPortal.conf";
            Config = Client.Converters.ModelSerializer.Deserialize<Config>(File.ReadAllText(configPath));

            var uri = Config.Server.Url;
            var project = Config.Server.Project;
            var password = Config.Server.Authentication.Uuid;

            IWebProxy proxy = null;

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

            Bridge.Service = new Service(uri, project, password);

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
            events.TestRunMessage += TestMessageHandler;

            events.TestResult += TestResultHandler;

            events.TestRunComplete += TestRunCompleteHandler;

        }

        /// <summary>
        /// Called when a test message is received.
        /// </summary>
        private void TestMessageHandler(object sender, TestRunMessageEventArgs e)
        {
            LogLevel logLevel = LogLevel.Debug;
            switch (e.Level)
            {
                case TestMessageLevel.Informational:
                    logLevel = LogLevel.Info;
                    break;

                case TestMessageLevel.Warning:
                    logLevel = LogLevel.Warning;
                    break;

                case TestMessageLevel.Error:
                    logLevel = LogLevel.Error;
                    break;
            }
            try
            {
                Bridge.LogMessage(logLevel, e.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\tException: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Called when a test result is received.
        /// </summary>
        private void TestResultHandler(object sender, TestResultEventArgs e)
        {
            if (!_isRunStarted)
            {
                RunStarted(e.Result);
                SuiteStarted("Default", e.Result);

                _isRunStarted = true;
            }
            TestStarted(e.Result.TestCase.DisplayName ?? e.Result.TestCase.FullyQualifiedName, e.Result.StartTime.UtcDateTime);

            TestFinished(e.Result);

            _lastTestResult = e.Result;
        }

        /// <summary>
        /// Called when a test run is completed.
        /// </summary
        private void TestRunCompleteHandler(object sender, TestRunCompleteEventArgs e)
        {
            SuiteFinished(_lastTestResult);
            RunFinished();
        }


        public void RunStarted(TestResult result)
        {
            var requestNewLaunch = new StartLaunchRequest
            {
                Name = Config.Launch.Name,
                StartTime = result.StartTime.UtcDateTime
            };
            if (Config.Launch.IsDebugMode)
            {
                requestNewLaunch.Mode = LaunchMode.Debug;
            }

            requestNewLaunch.Tags = Config.Launch.Tags;

            Bridge.Context.LaunchReporter = new LaunchReporter(Bridge.Service);
            Bridge.Context.LaunchReporter.Start(requestNewLaunch);
        }

        private TestReporter _suiteId;


        public void SuiteStarted(string name, TestResult result)
        {
            var requestNewSuite = new StartTestItemRequest
            {
                Name = name,
                StartTime = result.StartTime.UtcDateTime,
                Type = TestItemType.Suite
            };

            _suiteId = Bridge.Context.LaunchReporter.StartNewTestNode(requestNewSuite);
        }

        private TestReporter _testId;


        public void TestStarted(string testName, DateTime startTime)
        {
            if (Bridge.Context.LaunchReporter != null)
            {
                var requestNewTest = new StartTestItemRequest
                {
                    Name = testName,
                    StartTime = startTime,
                    Type = TestItemType.Step
                };
                try
                {
                    _testId = _suiteId.StartNewTestNode(requestNewTest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\tException: " + ex.Message);
                    throw;
                }
            }
        }

        public void TestFinished(TestResult result)
        {
            if (_testId != null)
            {
                foreach (var message in result.Messages)
                {
                    _testId.Log(new AddLogItemRequest
                    {
                        Time = DateTime.UtcNow,
                        Level = LogLevel.Info,
                        Text = message.Category + ":" + Environment.NewLine + message.Text
                    });
                }
            }

            if (result.ErrorMessage != null && _testId != null)
            {
                _testId.Log(new AddLogItemRequest
                {
                    Time = result.EndTime.UtcDateTime.AddMilliseconds(1),
                    Level = LogLevel.Error,
                    Text = result.ErrorMessage + "\n" + result.ErrorStackTrace
                });
            }
            if (_testId != null)
            {
                var description = result.TestCase.Traits.FirstOrDefault(x => x.Name == "Description");
                var requestUpdateTest = new UpdateTestItemRequest
                {
                    Description = description != null ? description.Value : String.Empty,
                    Tags = result.TestCase.Traits.Where(t=>t.Name.ToLower()=="Category".ToLower()).Select(x => x.Value).ToList()
                };
                _testId.Update(requestUpdateTest);

                var requestFinishTest = new FinishTestItemRequest
                {
                    EndTime = DateTime.UtcNow,
                    Status = _statusMap[result.Outcome]
                };
                _testId.Finish(requestFinishTest);
            }
        }

        public void SuiteFinished(TestResult result)
        {
            var requestFinishSuite = new FinishTestItemRequest
            {
                EndTime = result.EndTime.UtcDateTime.AddMilliseconds(1),
                Status = _statusMap[result.Outcome]
            };
            _suiteId.Finish(requestFinishSuite);
        }

        public void RunFinished(TestResult result)
        {
            RunFinished();
        }

        public void RunFinished(Exception exception)
        {
            RunFinished();
        }

        private void RunFinished()
        {
            if (Bridge.Context.LaunchReporter != null)
            {
                var requestFinishLaunch = new FinishLaunchRequest
                {
                    EndTime = DateTime.UtcNow
                };

                Bridge.Context.LaunchReporter.Finish(requestFinishLaunch);

                Stopwatch stopwatch = Stopwatch.StartNew();
                Console.WriteLine("Finishing to send results to Report Portal...");

                try
                {
                    Bridge.Context.LaunchReporter.FinishTask.Wait(TimeSpan.FromMinutes(30));
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp);
                    throw;
                }

                stopwatch.Stop();
                Console.WriteLine($"Results are sent to Report Portal. Sync time: {stopwatch.Elapsed}");

            }
        }
    }
}
