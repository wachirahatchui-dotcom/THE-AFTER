using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// The role of a member in a relationship
    /// </summary>
    [Preserve]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MemberRole
    {
        /// <summary>
        /// Enum Target for value: TARGET
        /// </summary>
        [EnumMember(Value = "TARGET")]
        Target = 1,
        [Obsolete("TARGET has been changed to Target. (UnityUpgradable) -> Target", true)]
        TARGET = -1,

        /// <summary>
        /// Enum Source for value: SOURCE
        /// </summary>
        [EnumMember(Value = "SOURCE")]
        Source = 2,
        [Obsolete("SOURCE has been changed to Source. (UnityUpgradable) -> Source", true)]
        SOURCE = -2,


        /// <summary>
        /// Enum None for value: NONE
        /// </summary>
        [EnumMember(Value = "NONE")]
        None = 3,
        [Obsolete("NONE has been changed to None. (UnityUpgradable) -> None", true)]
        NONE = -3
    }
}
