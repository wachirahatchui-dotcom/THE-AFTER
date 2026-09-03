using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// The current availability of the player.
    /// </summary>
    [Preserve]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Availability
    {
        /// <summary>
        /// Enum Unknown for value: UNKNOWN
        /// </summary>
        [EnumMember(Value = "UNKNOWN")]
        Unknown = 0,

        /// <summary>
        /// Enum Online for value: ONLINE
        /// </summary>
        [EnumMember(Value = "ONLINE")]
        Online = 1,

        /// <summary>
        /// Enum Busy for value: BUSY
        /// </summary>
        [EnumMember(Value = "BUSY")]
        Busy = 2,

        /// <summary>
        /// Enum Away for value: AWAY
        /// </summary>
        [EnumMember(Value = "AWAY")]
        Away = 3,

        /// <summary>
        /// Enum Invisible for value: INVISIBLE
        /// </summary>
        [EnumMember(Value = "INVISIBLE")]
        Invisible = 4,

        /// <summary>
        /// Enum Offline for value: OFFLINE
        /// </summary>
        [EnumMember(Value = "OFFLINE")]
        Offline = 5
    }
}
