using ReportPortal.Client.Abstractions.Models;
using System;

namespace ReportPortal.VSTest.TestLogger.LogHandler.Messages
{
    class AddLogCommunicationMessage : BaseCommunicationMessage
    {
        public override CommunicationAction Action { get => CommunicationAction.AddLog; set => base.Action = value; }

        public string ParentScopeId { get; set; }

        /// <summary>
        /// Date time of log item.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// A level of log item.
        /// </summary>
        public LogLevel Level = LogLevel.Info;

        /// <summary>
        /// Message of log item.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Specify an attachment of log item.
        /// </summary>
        public Attach Attach { get; set; }
    }

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

        public byte[] Data { get; set; }

        public string MimeType { get; set; }
    }
}
