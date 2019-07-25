using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using ReportPortal.Client.Models;
using ReportPortal.Client.Requests;

namespace ReportPortal.VSTest.TestLogger.Helpers
{
    public class LogRequestItemGenerator
    {
        public List<AddLogItemRequest> BuildLogItemRequest(ICollection<TestResultMessage> messages)
        {
            var listOfLogItemRequests = new List<AddLogItemRequest>();
            foreach (var message in messages)
            {
                if (message.Text == null) continue;
                foreach (var line in message.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var handled = false;

                    try
                    {
                        var sharedMessage =
                            Client.Converters.ModelSerializer.Deserialize<SharedLogMessage>(line);

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

                        listOfLogItemRequests.Add(logRequest);

                        handled = true;
                    }
                    catch (Exception)
                    {

                    }

                    if (!handled)
                    {
                        listOfLogItemRequests.Add(new AddLogItemRequest
                        {
                            Time = DateTime.UtcNow,
                            Level = LogLevel.Info,
                            Text = line
                        });
                    }
                }
            }

            return listOfLogItemRequests;
        }

        public IList<AddLogItemRequest> GetAttachmentRequest(ICollection<AttachmentSet> attachmentSets, DateTime timeResult)
        {
            var listOfAttachments = new List<AddLogItemRequest>();
            foreach (var attachmentSet in attachmentSets)
            {
                foreach (var attachmentData in attachmentSet.Attachments)
                {
                    var filePath = attachmentData.Uri.AbsolutePath;

                    if (!File.Exists(filePath))
                    {
                        listOfAttachments.Add(new AddLogItemRequest
                        {
                            Level = LogLevel.Warning,
                            Text = $"'{filePath}' file is not available.",
                            Time = timeResult
                        });
                    }
                    var fileExtension = Path.GetExtension(filePath);
                    var data = ReadFile(filePath, 2);
                    if (data != null)
                    {
                        listOfAttachments.Add(new AddLogItemRequest
                        {
                            Level = LogLevel.Info,
                            Text = Path.GetFileName(filePath),
                            Time = timeResult,
                            Attach = new Attach(Path.GetFileName(filePath), Shared.MimeTypes.MimeTypeMap.GetMimeType(fileExtension), data)
                        });
                    }
                    else
                    {
                        listOfAttachments.Add(new AddLogItemRequest
                        {
                            Level = LogLevel.Info,
                            Text = $"{Path.GetFileName(filePath)} in use. Attach of file wasn't completed correctly.",
                            Time = timeResult
                        });
                    }

                }
            }

            return listOfAttachments;
        }

        // Sometimes (for API and unit tests definitely) logs are still under use. Just proposition to add second retry for it.
        private byte[] ReadFile(string path, int retries)
        {
            byte[] data = null;
            for (var i = 1; i <= retries; i++)
            {
                try
                {
                    data = File.ReadAllBytes(path);
                }
                catch (IOException)
                {
                    Console.WriteLine($"File is not ready for reading. Will try again. Number of Retry: {i}");
                    Thread.Sleep(500);
                }
            }
            return data;
        }
    }
}