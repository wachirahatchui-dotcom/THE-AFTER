using System.Runtime.Serialization;
using UnityEngine.Scripting;
using Newtonsoft.Json;
using Unity.Services.Friends.Internal.Generated.Http;

namespace Unity.Services.Friends.Internal.Generated.Notifications
{
    /// <summary>
    /// Notification represents the notification received on a communication channel.
    /// </summary>
    [Preserve]
    [DataContract]
    internal class Notification
    {
        /// <summary>
        /// The creation time of the notification in UTC.
        /// </summary>
        [Preserve]
        [DataMember(Name = "created_at", IsRequired = true, EmitDefaultValue = false)]
        public string CreatedAt { get; internal set; }

        /// <summary>
        /// The request identifier of the notification.
        /// </summary>
        [Preserve]
        [DataMember(Name = "request_id", IsRequired = true, EmitDefaultValue = false)]
        public string RequestID { get; internal set; }

        /// <summary>
        /// The type of the notification.
        /// </summary>
        [Preserve]
        [DataMember(Name = "type", IsRequired = true, EmitDefaultValue = false)]
        public string Type { get; internal set; }

        /// <summary>
        /// The payload of the notification.
        /// </summary>
        [Preserve][JsonConverter(typeof(JsonObjectConverter))]
        [DataMember(Name = "payload", EmitDefaultValue = false)]
        public IDeserializable Payload { get; internal set; }
    }
}
