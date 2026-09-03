using System;
using System.Runtime.Serialization;
using UnityEngine.Scripting;
using Unity.Services.Friends.Internal.Generated.Http;
using Unity.Services.Friends.Internal.Generated.Models;
using Unity.Services.Friends.Models;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Returned when a PRESENCE_UPDATED event occurs on the notification system.
    /// </summary>
    [Preserve]
    [DataContract]
    internal class PresenceUpdatedEvent : IPresenceUpdatedEvent
    {
        internal const string k_PresenceUpdatedEventType = "PRESENCE_UPDATED";

        /// <summary>
        /// The member's identifier whose presence got updated.
        /// </summary>
        [Preserve]
        [DataMember(Name = "id", IsRequired = true, EmitDefaultValue = false)]
        internal string UserId;

        /// <summary>
        /// The member's presence whose presence got updated.
        /// </summary>
        [Preserve]
        [DataMember(Name = "presence", IsRequired = true, EmitDefaultValue = false)]
        internal UserPresence m_Presence;

        ///<inheritdoc cref="IPresenceUpdatedEvent.ID"/>
        public string ID => UserId;

        ///<inheritdoc cref="IPresenceUpdatedEvent.Presence"/>
        public Presence Presence
        {
            get
            {
                var presence = new Presence
                {
                    Availability = EnumParseHelper.FromInternalPresenceAvailabilityOptions(m_Presence.Availability),
                    LastSeen = m_Presence.LastSeen
                };
                presence.SetActivity(m_Presence.Activity);
                return presence;
            }
        }
    }
}
