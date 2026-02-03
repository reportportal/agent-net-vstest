using System;
using ReportPortal.Shared.Extensibility;
using ReportPortal.VSTest.TestLogger.LogHandler.Messages;
using ReportPortal.Shared.Extensibility.Commands;
using ReportPortal.Client.Abstractions.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace ReportPortal.VSTest.TestLogger
{
    public class BridgeExtension : ICommandsListener
    {
        public void Initialize(ICommandsSource commandsSource)
        {
            commandsSource.OnBeginLogScopeCommand += CommandsSource_OnBeginLogScopeCommand;
            commandsSource.OnEndLogScopeCommand += CommandsSource_OnEndLogScopeCommand;
            commandsSource.OnLogMessageCommand += CommandsSource_OnLogMessageCommand;
        }

        private void CommandsSource_OnLogMessageCommand(Shared.Execution.ILogContext logContext, Shared.Extensibility.Commands.CommandArgs.LogMessageCommandArgs args)
        {
            var logScope = args.LogScope;

            var communicationMessage = new AddLogCommunicationMessage()
            {
                ParentScopeId = logScope?.Id,
                Time = args.LogMessage.Time,
                Text = args.LogMessage.Message,
                Level = args.LogMessage.LevelString
            };

            if (args.LogMessage.Attachment != null)
            {
                communicationMessage.Attach = new Attach
                {
                    MimeType = args.LogMessage.Attachment.MimeType,
                    Data = args.LogMessage.Attachment.Data
                };
            }

            Console.WriteLine(JsonSerializer.Serialize(communicationMessage));
        }

        private void CommandsSource_OnEndLogScopeCommand(Shared.Execution.ILogContext logContext, Shared.Extensibility.Commands.CommandArgs.LogScopeCommandArgs args)
        {
            var logScope = args.LogScope;

            var communicationMessage = new EndScopeCommunicationMessage
            {
                Id = logScope.Id,
                EndTime = logScope.EndTime.Value,
                Status = logScope.Status
            };

            Console.WriteLine(JsonSerializer.Serialize(communicationMessage));
        }

        private void CommandsSource_OnBeginLogScopeCommand(Shared.Execution.ILogContext logContext, Shared.Extensibility.Commands.CommandArgs.LogScopeCommandArgs args)
        {
            var logScope = args.LogScope;

            var communicationMessage = new BeginScopeCommunicationMessage
            {
                Id = logScope.Id,
                ParentScopeId = logScope.Parent?.Id,
                Name = logScope.Name,
                BeginTime = logScope.BeginTime
            };

            Console.WriteLine(JsonSerializer.Serialize(communicationMessage));
        }
    }
}
