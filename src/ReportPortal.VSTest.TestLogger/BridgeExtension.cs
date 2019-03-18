using ReportPortal.Shared;
using System;
using ReportPortal.Client.Models;
using ReportPortal.Client.Requests;
using ReportPortal.Client.Converters;
using System.Runtime.Serialization;

namespace ReportPortal.VSTest.TestLogger
{
    public class BridgeExtension : IBridgeExtension
    {
        public bool Handled { get; set; }

        public int Order => int.MaxValue;

        public void FormatLog(ref AddLogItemRequest logRequest)
        {
            var sharedMessage = new SharedLogMessage()
            {
                TestItemId = logRequest.TestItemId,
                Time = logRequest.Time,
                Text = logRequest.Text,
                Level = logRequest.Level
            };
            if (logRequest.Attach != null)
            {
                sharedMessage.Attach = new SharedAttach
                {
                    Name = logRequest.Attach.Name,
                    MimeType = logRequest.Attach.MimeType,
                    Data = logRequest.Attach.Data
                };
            }

            Console.WriteLine(ModelSerializer.Serialize<SharedLogMessage>(sharedMessage));

            Handled = true;
        }
    }

    [DataContract]
    class SharedLogMessage
    {
        /// <summary>
        /// ID of test item to add new logs.
        /// </summary>
        [DataMember]
        public string TestItemId { get; set; }

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
        public SharedAttach Attach { get; set; }
    }

    [DataContract]
    public class SharedAttach
    {
        public SharedAttach()
        {

        }

        public SharedAttach(string name, string mimeType, byte[] data)
        {
            Name = name;
            MimeType = mimeType;
            Data = data;
        }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public byte[] Data { get; set; }

        [DataMember]
        public string MimeType { get; set; }
    }
}
