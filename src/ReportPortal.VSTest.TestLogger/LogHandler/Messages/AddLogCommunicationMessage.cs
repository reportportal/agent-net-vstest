using ReportPortal.Client.Abstractions.Models;
using System;
using System.Runtime.Serialization;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    [DataContract]
    class AddLogCommunicationMessage : BaseCommunicationMessage
    {
        public override CommunicationAction Action { get => CommunicationAction.AddLog; set => base.Action = value; }

        [DataMember]
        public string ParentScopeId { get; set; }

        /// <summary>
        /// Date time of log item.
        /// </summary>
        [DataMember]
        public DateTime Time { get; set; }

        /// <summary>
        /// A level of log item.
        /// </summary>
        [DataMember]
        public LogLevel Level = LogLevel.Info;

        /// <summary>
        /// Message of log item.
        /// </summary>
        [DataMember]
        public string Text { get; set; }

        /// <summary>
        /// Specify an attachment of log item.
        /// </summary>
        [DataMember]
        public Attach Attach { get; set; }
    }

    [DataContract]
    class Attach
    {
        public Attach()
        {

        }

        public Attach(string mimeType, byte[] data)
        {
            MimeType = mimeType;
            Data = data;
        }

        [DataMember]
        public byte[] Data { get; set; }

        [DataMember]
        public string MimeType { get; set; }
    }
}
