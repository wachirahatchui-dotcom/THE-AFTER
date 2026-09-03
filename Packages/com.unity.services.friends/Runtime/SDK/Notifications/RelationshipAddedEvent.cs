using System.Runtime.Serialization;
using InternalRelationship = Unity.Services.Friends.Internal.Generated.Models.Relationship;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Returned when a RELATIONSHIP_ADDED event occurs on the notification system.
    /// </summary>
    [Preserve]
    [DataContract]
    internal class RelationshipAddedEvent : IRelationshipAddedEvent
    {
        internal const string k_RelationshipAddedEventType = "RELATIONSHIP_ADDED";
        Relationship m_Relationship;

        internal RelationshipAddedEvent(InternalRelationship internalRelationship)
        {
            m_Relationship = WrappedFriendsApi.TransformResponseRelationship(internalRelationship, new MemberOptions());
        }

        ///<inheritdoc cref="IRelationshipAddedEvent.Relationship"/>
        public Relationship Relationship => m_Relationship;
    }
}
