using System.Runtime.Serialization;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    [DataContract]
    class BaseCommunicationMessage
    {
        [DataMember]
        public virtual CommunicationAction Action { get; set; }
    }
}
