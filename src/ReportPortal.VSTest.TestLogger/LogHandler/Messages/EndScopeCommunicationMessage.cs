using ReportPortal.Shared.Execution.Logging;
using System;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    class EndScopeCommunicationMessage : BaseCommunicationMessage
    {
        public override CommunicationAction Action { get => CommunicationAction.EndLogScope; set => base.Action = value; }

        public string Id { get; set; }

        public DateTime EndTime { get; set; }

        public LogScopeStatus Status { get; set; }

    }
}
