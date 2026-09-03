using System;
using System.Linq;
using System.Runtime.Serialization;
using Unity.Services.Friends.Internal.Generated.Models;
using InternalRelationshipType = Unity.Services.Friends.Internal.Generated.Models.RelationshipType;

namespace Unity.Services.Friends.Models
{
    static class EnumParseHelper
    {
        internal static InternalRelationshipType ToInternalRelationshipType(RelationshipType type)
        {
            return type switch
            {
                RelationshipType.FriendRequest => InternalRelationshipType.FRIENDREQUEST,
                RelationshipType.Friend => InternalRelationshipType.FRIEND,
                RelationshipType.Block => InternalRelationshipType.BLOCK,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        internal static RelationshipType FromInternalRelationshipType(InternalRelationshipType type)
        {
            return type switch
            {
                InternalRelationshipType.FRIEND => RelationshipType.Friend,
                InternalRelationshipType.BLOCK => RelationshipType.Block,
                InternalRelationshipType.FRIENDREQUEST => RelationshipType.FriendRequest,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        internal static PresenceRequest.AvailabilityOptions ToInternalPresenceAvailabilityOptions(
            Availability options)
        {
            return options switch
            {
                Availability.Online => PresenceRequest.AvailabilityOptions.ONLINE,
                Availability.Busy => PresenceRequest.AvailabilityOptions.BUSY,
                Availability.Away => PresenceRequest.AvailabilityOptions.AWAY,
                Availability.Invisible => PresenceRequest.AvailabilityOptions.INVISIBLE,
                Availability.Offline => PresenceRequest.AvailabilityOptions.OFFLINE,
                Availability.Unknown => PresenceRequest.AvailabilityOptions.OFFLINE,
                _ => throw new ArgumentOutOfRangeException(nameof(options), options, null)
            };
        }

        internal static Availability FromInternalPresenceAvailabilityOptions(
            UserPresence.AvailabilityOptions options)
        {
            return options switch
            {
                UserPresence.AvailabilityOptions.ONLINE => Availability.Online,
                UserPresence.AvailabilityOptions.BUSY => Availability.Busy,
                UserPresence.AvailabilityOptions.AWAY => Availability.Away,
                UserPresence.AvailabilityOptions.OFFLINE => Availability.Offline,
                UserPresence.AvailabilityOptions.UNKNOWN => Availability.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(options), options, null)
            };
        }

        internal static MemberRole FromInternalMemberRole(Role role)
        {
            return role switch
            {
                Role.SOURCE => MemberRole.Source,
                Role.TARGET => MemberRole.Target,
                Role.NONE => MemberRole.None,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            };
        }

        internal static string GetEnumMemberAttrValue<T>(T enumVal)
        {
            var enumType = typeof(T);
            var memInfo = enumType.GetMember(enumVal.ToString());
            var attr = memInfo.FirstOrDefault()?.GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return attr?.Value;
        }
    }
}
