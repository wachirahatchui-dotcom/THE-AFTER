using System.Runtime.Serialization;
using UnityEngine.Scripting;
using Unity.Services.Friends.Internal.Generated.Http;
using MissingMemberHandling = Unity.Services.Friends.Internal.Generated.Http.MissingMemberHandling;

namespace Unity.Services.Friends.Notifications
{
    ///<inheritdoc cref="IMessageReceivedEvent"/>
    [Preserve]
    [DataContract]
    class MessageReceivedEvent : IMessageReceivedEvent
    {
        internal const string k_MessageReceivedEventType = "MESSAGE";

        /// <summary>
        /// The member's identifier whose presence got updated.
        /// </summary>
        [Preserve][DataMember(Name = "id", IsRequired = true, EmitDefaultValue = false)]
        internal string Id;

        /// <summary>
        /// The message received. The maximum size of the message payload is 10 kilobytes.
        /// </summary>
        [Preserve][DataMember(Name = "message", IsRequired = true, EmitDefaultValue = true)]
        internal IDeserializable Message;

        ///<inheritdoc cref="IMessageReceivedEvent.UserId"/>
        public string UserId => Id;

        ///<inheritdoc cref="IMessageReceivedEvent.GetAs"/>
        public T GetAs<T>() where T : new()
        {
            return Message.GetAs<T>(new DeserializationSettings { MissingMemberHandling = MissingMemberHandling.Ignore });
        }
    }
}
