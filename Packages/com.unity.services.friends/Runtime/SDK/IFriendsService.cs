using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;

namespace Unity.Services.Friends
{
    /// <summary>
    /// FriendsService is the managed service that let's user's interact with the Friends package's features
    /// </summary>
    public interface IFriendsService : IMessagingService
    {
        /// <summary>
        /// The full list of the user's relationships.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        IReadOnlyList<Relationship> Relationships { get; }

        /// <summary>
        /// The list of the user's incoming friend requests.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        IReadOnlyList<Relationship> IncomingFriendRequests { get; }

        /// <summary>
        /// The list of the user's outgoing friend requests.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        IReadOnlyList<Relationship> OutgoingFriendRequests { get; }

        /// <summary>
        /// The list of the user's friends.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        IReadOnlyList<Relationship> Friends { get; }

        /// <summary>
        /// The list of the user's blocks.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        IReadOnlyList<Relationship> Blocks { get; }

        /// <summary>
        /// Initialize the Friends service API.
        /// This must be called before using any other functionality of the Friends service.
        /// This can only be called when a user is signed in.
        /// </summary>
        /// <param name="initializeOptions">Options to initialize the Friends service</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task InitializeAsync(InitializeOptions initializeOptions = null);

        /// <summary>
        /// Creates a friend request, or automatically creates a friendship if the user already has an incoming friend request from the
        /// targeted user.
        /// </summary>
        /// <param name="memberId">The ID of the target user.</param>
        /// <returns>The friendship created</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task<Relationship> AddFriendAsync(string memberId);

        /// <summary>
        /// Creates a friend request, or automatically creates a friendship if the user already has an incoming friend request from the
        /// targeted user by their user name.
        /// </summary>
        /// <param name="name">The name of the target user</param>
        /// <returns>The friendship created</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task<Relationship> AddFriendByNameAsync(string name);

        /// <summary>
        /// Creates a block towards the targeted user.
        /// </summary>
        /// <param name="memberId">The ID of the target user.</param>
        /// <returns>The block created</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task<Relationship> AddBlockAsync(string memberId);

        /// <summary>
        /// Deletes an incoming friend request.
        /// </summary>
        /// <param name="memberId">The ID of the user that sent the friend request.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.RelationshipNotFoundException">Represents an error that occurs when a relationship is not found.</exception>
        Task DeleteIncomingFriendRequestAsync(string memberId);

        /// <summary>
        /// Deletes an outgoing friend request.
        /// </summary>
        /// <param name="memberId">The ID of the user that a friend request was sent to.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.RelationshipNotFoundException">Represents an error that occurs when a relationship is not found.</exception>
        Task DeleteOutgoingFriendRequestAsync(string memberId);

        /// <summary>
        /// Deletes a friend.
        /// </summary>
        /// <param name="memberId">The ID of the friend to be deleted.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.RelationshipNotFoundException">Represents an error that occurs when a relationship is not found.</exception>
        Task DeleteFriendAsync(string memberId);

        /// <summary>
        /// Deletes a block.
        /// </summary>
        /// <param name="memberId">The ID of the user that will be unblocked.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.RelationshipNotFoundException">Represents an error that occurs when a relationship is not found.</exception>
        Task DeleteBlockAsync(string memberId);

        /// <summary>
        /// Deletes a relationship.
        /// </summary>
        /// <param name="relationshipId">The ID of the relationship to be deleted.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task DeleteRelationshipAsync(string relationshipId);

        /// <summary>
        /// Sets the user's presence availability.
        /// </summary>
        /// <param name="availabilityOption">The type of availability to be set for the user's presence.</param>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task SetPresenceAvailabilityAsync(Availability availabilityOption);

        /// <summary>
        /// Sets the user's presence activity value.
        /// </summary>
        /// <param name="activity">The activity value to be set for the user's presence.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task SetPresenceActivityAsync<T>(T activity) where T : new();

        /// <summary>
        /// Sets the presence for the user.
        /// </summary>
        /// <param name="availabilityOption">The type of availability to be set for the user's presence.</param>
        /// <param name="activity">The activity value to be set for the user's presence.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly setup.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task SetPresenceAsync<T>(Availability availabilityOption, T activity) where T : new();

        /// <summary>
        /// Forcefully refreshes the list of relationships in case they were not refreshed automatically by the service
        /// </summary>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        Task ForceRelationshipsRefreshAsync();

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
        /// Event called when a message is received.
        /// </summary>
        public event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// Event called when a the connectivity to the friends notification system has changed.
        /// </summary>
        public event Action<INotificationsStateChangedEvent> NotificationsConnectivityChanged;
    }
}
