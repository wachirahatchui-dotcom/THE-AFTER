using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// Deprecated.
    /// </summary>
    [Obsolete("PresenceAvailabilityOptions has been changed to Availability. (UnityUpgradable) -> Availability", true)]
    [Preserve]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PresenceAvailabilityOptions
    {
        /// <summary>
        /// Enum ONLINE for value: ONLINE
        /// </summary>
        [Obsolete("ONLINE has been changed to Online. (UnityUpgradable) -> Availability.Online", true)]
        [EnumMember(Value = "ONLINE")]
        ONLINE = 1,

        /// <summary>
        /// Enum BUSY for value: BUSY
        /// </summary>
        [Obsolete("BUSY has been changed to Busy. (UnityUpgradable) -> Availability.Busy", true)]
        [EnumMember(Value = "BUSY")]
        BUSY = 2,

        /// <summary>
        /// Enum AWAY for value: AWAY
        /// </summary>
        [Obsolete("AWAY has been changed to Away. (UnityUpgradable) -> Availability.Away", true)]
        [EnumMember(Value = "AWAY")]
        AWAY = 3,

        /// <summary>
        /// Enum INVISIBLE for value: INVISIBLE
        /// </summary>
        [Obsolete("INVISIBLE has been changed to Invisible. (UnityUpgradable) -> Availability.Invisible", true)]
        [EnumMember(Value = "INVISIBLE")]
        INVISIBLE = 4,

        /// <summary>
        /// Enum OFFLINE for value: OFFLINE
        /// </summary>
        [Obsolete("OFFLINE has been changed to Offline. (UnityUpgradable) -> Availability.Offline", true)]
        [EnumMember(Value = "OFFLINE")]
        OFFLINE = 5,

        /// <summary>
        /// Enum UNKNOWN for value: UNKNOWN
        /// </summary>
        [Obsolete("UNKNOWN has been changed to Unknown. (UnityUpgradable) -> Availability.Unknown", true)]
        [EnumMember(Value = "UNKNOWN")]
        UNKNOWN = 6
    }
}
