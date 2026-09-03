using System;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// A class for you to provide the callbacks you want to be called from the events subscription.
    /// </summary>
    public class FriendsEventCallbacks
    {
        /// <summary>
        /// Event called when the connection state of the events subscription changes.
        /// </summary>
        public event Action<FriendsEventConnectionState> FriendsEventConnectionStateChanged;

        /// <summary>
        /// Event called when a relationship gets added.
        /// </summary>
        public event Action<IRelationshipAddedEvent> RelationshipAdded;

        /// <summary>
        /// Event called when a relationship gets deleted.
        /// </summary>
        public event Action<IRelationshipDeletedEvent> RelationshipDeleted;

        /// <summary>
        /// Event called when a friend's presence is updated.
        /// </summary>
        public event Action<IPresenceUpdatedEvent> PresenceUpdated;

        /// <summary>
        /// Event called when a message is received
        /// </summary>
        public event Action<IMessageReceivedEvent> MessageReceived;

        internal void InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState state)
        {
            FriendsEventConnectionStateChanged?.Invoke(state);
        }

        /// <summary>
        /// Invoked when a RELATIONSHIP_ADDED event occurs on the notification channel.
        /// </summary>
        /// <param name="relationshipAddedEvent">The payload of the RELATIONSHIP_ADDED event.</param>
        internal void InvokeRelationshipAdded(IRelationshipAddedEvent relationshipAddedEvent)
        {
            RelationshipAdded?.Invoke(relationshipAddedEvent);
        }

        /// <summary>
        /// Invoked when a RELATIONSHIP_DELETED event occurs on the notification channel.
        /// </summary>
        /// <param name="relationshipDeletedEvent">The payload of the RELATIONSHIP_DELETED event.</param>
        internal void InvokeRelationshipDeleted(IRelationshipDeletedEvent relationshipDeletedEvent)
        {
            RelationshipDeleted?.Invoke(relationshipDeletedEvent);
        }

        /// <summary>
        /// Invoked when a PRESENCE_UPDATED event occurs on the notification channel.
        /// </summary>
        /// <param name="presenceUpdatedEvent">The payload of the PRESENCE_UPDATED event.</param>
        internal void InvokePresenceUpdated(IPresenceUpdatedEvent presenceUpdatedEvent)
        {
            PresenceUpdated?.Invoke(presenceUpdatedEvent);
        }

        /// <summary>
        /// Invoked when a MESSAGE event occurs on the notification channel.
        /// </summary>
        /// <param name="messageReceivedEvent">The payload of the MESSAGE event.</param>
        internal void InvokeMessageReceived(IMessageReceivedEvent messageReceivedEvent)
        {
            MessageReceived?.Invoke(messageReceivedEvent);
        }
    }
}
