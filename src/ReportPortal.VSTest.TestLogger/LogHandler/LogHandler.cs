using System;
using ReportPortal.Client.Converters;
using ReportPortal.Shared.Extensibility;
using ReportPortal.Client.Abstractions.Requests;
using ReportPortal.Shared.Logging;
using ReportPortal.VSTest.TestLogger.LogHandler.Messages;

namespace ReportPortal.VSTest.TestLogger
{
    public class BridgeExtension : ILogHandler
    {
        public int Order => 100;

        public void BeginScope(ILogScope logScope)
        {
            var communicationMessage = new BeginScopeCommunicationMessage
            {
                Id = logScope.Id,
                ParentScopeId = logScope.Parent?.Id,
                Name = logScope.Name,
                BeginTime = logScope.BeginTime
            };

            Console.WriteLine(ModelSerializer.Serialize<BeginScopeCommunicationMessage>(communicationMessage));
        }

        public void EndScope(ILogScope logScope)
        {
            var communicationMessage = new EndScopeCommunicationMessage
            {
                Id = logScope.Id,
                EndTime = logScope.EndTime.Value,
                Status = logScope.Status
            };

            Console.WriteLine(ModelSerializer.Serialize<EndScopeCommunicationMessage>(communicationMessage));
        }

        public bool Handle(ILogScope logScope, CreateLogItemRequest logRequest)
        {
            var communicationMessage = new AddLogCommunicationMessage()
            {
                ParentScopeId = logScope?.Id,
                Time = logRequest.Time,
                Text = logRequest.Text,
                Level = logRequest.Level
            };

            if (logRequest.Attach != null)
            {
                communicationMessage.Attach = new Attach
                {
                    Name = logRequest.Attach.Name,
                    MimeType = logRequest.Attach.MimeType,
                    Data = logRequest.Attach.Data
                };
            }

            Console.WriteLine(ModelSerializer.Serialize<AddLogCommunicationMessage>(communicationMessage));

            return true;
        }
    }
}
