using System;
using System.Runtime.Serialization;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    [DataContract]
    class BeginScopeCommunicationMessage : BaseCommunicationMessage
    {
        public override CommunicationAction Action { get => CommunicationAction.BeginLogScope; set => base.Action = value; }

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string ParentScopeId { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public DateTime BeginTime { get; set; }
    }
}
