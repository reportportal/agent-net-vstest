using System;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    class BeginScopeCommunicationMessage : BaseCommunicationMessage
    {
        public override CommunicationAction Action { get => CommunicationAction.BeginLogScope; set => base.Action = value; }

        public string Id { get; set; }

        public string ParentScopeId { get; set; }

        public string Name { get; set; }

        public DateTime BeginTime { get; set; }
    }
}
