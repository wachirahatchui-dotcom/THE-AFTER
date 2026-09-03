using System;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Core;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Wire.Internal;
using Unity.Services.Friends.Internal.Generated.Notifications;
using InternalRelationship = Unity.Services.Friends.Internal.Generated.Models.Relationship;


namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// A class for you to provide the callbacks to invoke for the Friends events, a way to subscribe and unsubscribe
    /// to the Friends events and get real time updates on the connection state of the Friends notification system.
    /// </summary>
    class FriendsChannel : IFriendsEvents
    {
        /// <summary>
        /// This object allows the subscription to a channel.
        /// </summary>
        readonly IChannel m_Channel;

        /// <summary>
        /// This object offer the methods to call when events occur on the channel.
        /// </summary>
        public FriendsEventCallbacks Callbacks { get; }

        /// <summary>
        /// Get the Friends base path to connect to when doing API calls.
        /// </summary>
        /// <param name="channel">This object allows the subscription to a channel.</param>
        /// <param name="callbacks">The callbacks you want to be called from the events subscription.</param>
        internal FriendsChannel(IChannel channel, FriendsEventCallbacks callbacks)
        {
            m_Channel = channel;
            Callbacks = callbacks;
            m_Channel.MessageReceived += (payload) => { OnSubscriptionMessage(payload, callbacks); };
            m_Channel.NewStateReceived += (state) => { OnSubscriptionNewState(state, callbacks); };
        }

        ///<inheritdoc cref="IFriendsEvents.SubscribeAsync"/>
        public async Task SubscribeAsync()
        {
            try
            {
                await m_Channel.SubscribeAsync();
            }
            catch (RequestFailedException ex)
            {
                throw ex.ErrorCode switch
                {
                    23003 => new FriendsServiceException(HttpStatusCode.BadGateway,
                        $"The connection to the notification system failed.", ex, FriendsErrorCode.NotificationConnectionError),
                    23008 => new FriendsServiceException(HttpStatusCode.BadGateway,
                        $"Notification system is already in a subscribed state.", ex, FriendsErrorCode.NotificationConnectionError),
                    _ => new FriendsServiceException(HttpStatusCode.InternalServerError,
                        DefaultErrorMessage(ex.ErrorCode), ex, FriendsErrorCode.NotificationConnectionError)
                };
            }
        }

        ///<inheritdoc cref="IRelationshipsEvents.UnsubscribeAsync"/>
        public async Task UnsubscribeAsync()
        {
            try
            {
                await m_Channel.UnsubscribeAsync();
            }
            catch (RequestFailedException ex)
            {
                throw ex.ErrorCode switch
                {
                    //ToDo possibly add this to console
                    23009 => new FriendsServiceException(HttpStatusCode.Conflict,
                        $"Notification system is already in an unsubscribed state.", ex, FriendsErrorCode.NotificationConnectionError),
                    _ => new FriendsServiceException(HttpStatusCode.InternalServerError,
                        DefaultErrorMessage(ex.ErrorCode), ex, FriendsErrorCode.NotificationConnectionError)
                };
            }
        }

        /// <summary>
        /// Invoked when a new message is received on the notification channel.
        /// </summary>
        /// <param name="payload">The payload received on the notification channel.</param>
        /// <param name="callbacks">The callbacks you want to be called from the events subscription.</param>
        void OnSubscriptionMessage(string payload, FriendsEventCallbacks callbacks)
        {
            try
            {
                var notification = JsonConvert.DeserializeObject<Notification>(payload);

                switch (notification?.Type)
                {
                    case RelationshipAddedEvent.k_RelationshipAddedEventType:
                        callbacks.InvokeRelationshipAdded(new RelationshipAddedEvent(notification.Payload.GetAs<InternalRelationship>()));
                        break;
                    case RelationshipDeletedEvent.k_RelationshipDeletedEventType:
                        callbacks.InvokeRelationshipDeleted(new RelationshipDeletedEvent(notification.Payload.GetAs<InternalRelationship>()));
                        break;
                    case PresenceUpdatedEvent.k_PresenceUpdatedEventType:
                        callbacks.InvokePresenceUpdated(notification.Payload.GetAs<PresenceUpdatedEvent>());
                        break;
                    case MessageReceivedEvent.k_MessageReceivedEventType:
                        callbacks.InvokeMessageReceived(notification.Payload.GetAs<MessageReceivedEvent>());
                        break;
                    default:
                        throw new FriendsServiceException(HttpStatusCode.InternalServerError,
                            "An unknown notification was received from the notification system.", null, FriendsErrorCode.NotificationUnknown);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        /// <summary>
        /// Event called when the connection state of the friends event subscription changes.
        /// </summary>
        /// <param name="state">The new state the notification system is currently in.</param>
        /// <param name="callbacks">The callbacks you want to be called from the friends event subscription.</param>
        void OnSubscriptionNewState(SubscriptionState state, FriendsEventCallbacks callbacks)
        {
            switch (state)
            {
                case SubscriptionState.Unsubscribed: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Unsubscribed); break;
                case SubscriptionState.Subscribing: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Subscribing); break;
                case SubscriptionState.Synced: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Subscribed); break;
                case SubscriptionState.Unsynced: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Unsynced); break;
                case SubscriptionState.Error: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Error); break;
                // Currently, it's impossible to reach the default case.
                default: callbacks.InvokeRelationshipsEventConnectionStateChanged(FriendsEventConnectionState.Unknown); break;
            }
        }

        /// <summary>
        /// Default error message string builder to centralize errors raised by the FriendsChannel based on the Wire error code.
        /// </summary>
        /// <param name="wireErrorCode">The error code received by the Wire notification system.</param>
        static string DefaultErrorMessage(int wireErrorCode)
        {
            return
                $"There was an error when trying to connect to the friends service for notifications. (notification error code: {wireErrorCode})";
        }
    }
}
