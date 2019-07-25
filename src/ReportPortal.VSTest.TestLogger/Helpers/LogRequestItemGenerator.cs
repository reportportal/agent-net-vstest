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

                    // Sometimes (for API and unit tests definitely) logs are still under use. Just proposition to add second retry for it.
                    var retry = 2;
                    for (var i = 1; i <= retry; i++)
                    {
                        try
                        {
                            var data = File.ReadAllBytes(filePath);
                            listOfAttachments.Add(new AddLogItemRequest
                            {
                                Level = LogLevel.Info,
                                Text = Path.GetFileName(filePath),
                                Time = timeResult,
                                Attach = new Attach(Path.GetFileName(filePath), Shared.MimeTypes.MimeTypeMap.GetMimeType(fileExtension), data)
                            });
                            break;
                        }
                        catch (IOException ex)
                        {
                            if (i < retry)
                            {
                                listOfAttachments.Add(new AddLogItemRequest
                                {
                                    Level = LogLevel.Warning,
                                    Text = $"{Path.GetFileName(filePath)} file is not ready for reading. Will try again. Number of Retry: {i}. Attach of file wasn't completed correctly. \n {ex.Message}",
                                    Time = timeResult
                                });
                                Thread.Sleep(500);
                            }
                            else
                            {
                                listOfAttachments.Add(new AddLogItemRequest
                                {
                                    Level = LogLevel.Warning,
                                    Text = $"{Path.GetFileName(filePath)} still in use. Attach of file wasn't completed correctly.\n{ex.Message}",
                                    Time = timeResult
                                });
                            }
                        }
                    }
                }
            }

            return listOfAttachments;
        }
    }
}