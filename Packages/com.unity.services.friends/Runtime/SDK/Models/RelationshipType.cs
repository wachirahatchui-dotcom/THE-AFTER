using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// The relationship type between members
    /// </summary>
    [Preserve]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RelationshipType
    {
        /// <summary>
        /// Enum Friend for value: FRIEND
        /// </summary>
        [EnumMember(Value = "FRIEND")]
        Friend = 1,
        [Obsolete("FRIEND has been changed to Friend. (UnityUpgradable) -> Friend", true)]
        FRIEND = -1,

        /// <summary>
        /// Enum Block for value: BLOCK
        /// </summary>
        [EnumMember(Value = "BLOCK")]
        Block = 2,
        [Obsolete("BLOCK has been changed to Block. (UnityUpgradable) -> Block", true)]
        BLOCK = -2,

        /// <summary>
        /// Enum FriendRequest for value: FRIEND_REQUEST
        /// </summary>
        [EnumMember(Value = "FRIEND_REQUEST")]
        FriendRequest = 3,
        [Obsolete("FRIEND_REQUEST has been changed to FriendRequest. (UnityUpgradable) -> FriendRequest", true)]
        FRIEND_REQUEST = -3
    }
}
