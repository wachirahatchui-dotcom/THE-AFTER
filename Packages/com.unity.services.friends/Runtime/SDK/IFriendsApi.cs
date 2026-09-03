using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;

namespace Unity.Services.Friends
{
    /// <summary>
    /// FriendsApi is the low level API wrapper for the Friends service
    /// </summary>
    interface IFriendsApi : IMessagingService, IDisposable
    {
        /// <summary>
        /// Async Operation.
        /// Retrieve the current user's relationships list.
        /// </summary>
        /// <param name="relationshipOptions">Relationship query options</param>
        /// <returns>List of relationships</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occurs when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task<List<Relationship>> GetRelationshipsAsync(RelationshipOptions relationshipOptions);

        /// <summary>
        /// Async Operation.
        /// Create a relationship between the current user and another user.
        /// </summary>
        /// <param name="memberId">The user the current user is targeting to create a relationship with</param>
        /// <param name="relationshipType">The type of relationship to be created</param>
        /// <param name="memberOptions">Options to add extra user information when the created relationship is returned</param>
        /// <returns>The relationship that was created</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occur when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task<Relationship> AddRelationshipAsync(string memberId, RelationshipType relationshipType, MemberOptions memberOptions);

        /// <summary>
        /// Async Operation.
        /// Create a friendship relationship using the target user's name
        /// </summary>
        /// <param name="name">The name of the user the current user is targeting to create a friendship with</param>
        /// <param name="memberOptions">Options to add extra user information when the created friendship is returned</param>
        /// <returns>The friendship that was created</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occur when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task<Relationship> AddFriendByNameAsync(string name, MemberOptions memberOptions);

        /// <summary>
        /// Async Operation.
        /// Remove a relationship.
        /// </summary>
        /// <param name="relationshipId">Identifier of the relationship</param>
        /// <returns>Awaitable task</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occur when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task DeleteRelationshipAsync(string relationshipId);

        /// <summary>
        /// Async Operation.
        /// Setter for the current user's presence
        /// </summary>
        /// <param name="availabilityOption">Availability status to be set for the current user</param>
        /// <param name="activity">Activity type T object</param>
        /// <returns>Awaitable task</returns>
        /// <exception cref="System.ArgumentException">Represents an error that occur when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task SetPresenceAsync<T>(Availability availabilityOption, T activity) where T : new();

        /// <summary>
        /// Async Operation.
        /// Subscribe to events async.
        /// </summary>
        /// <param name="callbacks">The callbacks you provide, which will be called as notifications arrive from the subscription.</param>
        /// <returns>An interface allowing the subscription and unsubscription to the Friends notification system</returns>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task<IFriendsEvents> SubscribeToEventsAsync(FriendsEventCallbacks callbacks);
    }
}
