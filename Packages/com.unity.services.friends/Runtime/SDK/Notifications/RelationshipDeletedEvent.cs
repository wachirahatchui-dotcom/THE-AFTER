using System.Runtime.Serialization;
using InternalRelationship = Unity.Services.Friends.Internal.Generated.Models.Relationship;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using UnityEngine.Scripting;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Returned when a RELATIONSHIP_DELETED event occurs on the notification system.
    /// </summary>
    [Preserve]
    [DataContract]
    internal class RelationshipDeletedEvent : IRelationshipDeletedEvent
    {
        internal const string k_RelationshipDeletedEventType = "RELATIONSHIP_DELETED";
        Relationship m_Relationship;

        internal RelationshipDeletedEvent(InternalRelationship internalRelationship)
        {
            m_Relationship = WrappedFriendsApi.TransformResponseRelationship(internalRelationship, new MemberOptions());
        }

        ///<inheritdoc cref="IRelationshipDeletedEvent.Relationship"/>
        public Relationship Relationship => m_Relationship;
    }
}
