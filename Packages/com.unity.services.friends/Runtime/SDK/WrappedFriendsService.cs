using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using UnityEngine;
using InternalFriendsService = Unity.Services.Friends.Internal.Generated.FriendsService;

namespace Unity.Services.Friends
{
    ///<inheritdoc cref="IFriendsService"/>
    sealed class WrappedFriendsService : IFriendsService
    {
        public event Action<IRelationshipAddedEvent> RelationshipAdded;

        public event Action<IRelationshipDeletedEvent> RelationshipDeleted;

        public event Action<IPresenceUpdatedEvent> PresenceUpdated;

        public event Action<IMessageReceivedEvent> MessageReceived;

        public event Action<INotificationsStateChangedEvent> NotificationsConnectivityChanged;


        public IReadOnlyList<Relationship> Relationships
        {
            get
            {
                ValidateInitialized();
                return m_Relationships as IReadOnlyList<Relationship>;
            }
        }

        public IReadOnlyList<Relationship> IncomingFriendRequests
        {
            get
            {
                ValidateInitialized();
                return m_Relationships
                    .Where(r => r.Type == RelationshipType.FriendRequest
                    && r.Member.Role == MemberRole.Source
                    && !Blocks.Any(b => b.Member.Id == r.Member.Id))
                    .ToList();
            }
        }

        public IReadOnlyList<Relationship> OutgoingFriendRequests
        {
            get
            {
                ValidateInitialized();
                return m_Relationships
                    .Where(r => r.Type == RelationshipType.FriendRequest
                    && r.Member.Role == MemberRole.Target
                    && !Blocks.Any(b => b.Member.Id == r.Member.Id))
                    .ToList();
            }
        }

        public IReadOnlyList<Relationship> Blocks
        {
            get
            {
                ValidateInitialized();
                return m_Relationships
                    .Where(r => r.Type == RelationshipType.Block)
                    .ToList();
            }
        }

        public IReadOnlyList<Relationship> Friends
        {
            get
            {
                ValidateInitialized();
                return m_Relationships
                    .Where(r => r.Type == RelationshipType.Friend
                    && !Blocks.Any(b => b.Member.Id == r.Member.Id))
                    .ToList();
            }
        }

        IList<Relationship> m_Relationships = new List<Relationship>();

        readonly IFriendsApi m_FriendsApi;

        bool m_Initialized { get; set; }

        IFriendsEvents m_Events;

        object m_PresenceActivity;

        Availability m_Availability;

        InitializeOptions m_InitializeOptions;

        Task m_CleanupTask = Task.CompletedTask;

        internal WrappedFriendsService(IFriendsApi friendsApi)
        {
            m_FriendsApi = friendsApi;
        }

        ///<inheritdoc cref="IFriendsService.AddFriendAsync"/>
        public async Task InitializeAsync(InitializeOptions initializeOptions = null)
        {
            if (!m_Initialized)
            {
                m_InitializeOptions = initializeOptions ?? new InitializeOptions();
                await m_CleanupTask;

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    throw new InvalidOperationException($"Unable to call {nameof(FriendsService)}.{nameof(InitializeAsync)}  because no user is signed in.");
                }

                await InitializeRelationshipsAsync();
                m_PresenceActivity = new WrappedFriendsApi.BlankActivity();
                m_Availability = Availability.Unknown;
                if (m_InitializeOptions.UseEvents)
                {
                    await InitializeEventSubscriptionAsync();
                }

                m_Initialized = true;
            }
        }

        ///<inheritdoc cref="IFriendsService.AddFriendAsync"/>
        public async Task<Relationship> AddFriendAsync(string memberId)
        {
            ValidateInitialized();
            return await AddRelationshipAsync(memberId, RelationshipType.Friend);
        }

        ///<inheritdoc cref="IFriendsService.AddFriendByNameAsync"/>
        public async Task<Relationship> AddFriendByNameAsync(string name)
        {
            ValidateInitialized();
            var relationship = await m_FriendsApi.AddFriendByNameAsync(name, new MemberOptions());

            m_Relationships.Remove(Relationships.FirstOrDefault(r =>
                r.Type == RelationshipType.FriendRequest &&
                r.Member.Profile.Name == name));

            m_Relationships.Add(relationship);

            return relationship;
        }

        ///<inheritdoc cref="IFriendsService.AddBlockAsync"/>
        public async Task<Relationship> AddBlockAsync(string memberId)
        {
            ValidateInitialized();
            return await AddRelationshipAsync(memberId, RelationshipType.Block);
        }

        ///<inheritdoc cref="IFriendsService.DeleteIncomingFriendRequestAsync"/>
        public async Task DeleteIncomingFriendRequestAsync(string memberId)
        {
            ValidateInitialized();
            var incomingFriendRequest = Relationships.FirstOrDefault(r =>
                r.Type == RelationshipType.FriendRequest &&
                r.Member.Role == MemberRole.Source &&
                r.Member.Id == memberId);
            if (incomingFriendRequest == null)
                throw new RelationshipNotFoundException(memberId);
            await DeleteRelationshipAsync(incomingFriendRequest.Id);
        }

        ///<inheritdoc cref="IFriendsService.DeleteOutgoingFriendRequestAsync"/>
        public async Task DeleteOutgoingFriendRequestAsync(string memberId)
        {
            ValidateInitialized();
            var outgoingFriendRequest = Relationships.FirstOrDefault(r =>
                r.Type == RelationshipType.FriendRequest &&
                r.Member.Role == MemberRole.Target &&
                r.Member.Id == memberId);
            if (outgoingFriendRequest == null)
                throw new RelationshipNotFoundException(memberId);
            await DeleteRelationshipAsync(outgoingFriendRequest.Id);
        }

        ///<inheritdoc cref="IFriendsService.DeleteFriendAsync"/>
        public async Task DeleteFriendAsync(string memberId)
        {
            ValidateInitialized();
            var friendship = Relationships.FirstOrDefault(r =>
                r.Type == RelationshipType.Friend && r.Member.Id == memberId);
            if (friendship == null)
                throw new RelationshipNotFoundException(memberId);
            await DeleteRelationshipAsync(friendship.Id);
        }

        ///<inheritdoc cref="IFriendsService.DeleteBlockAsync"/>
        public async Task DeleteBlockAsync(string memberId)
        {
            ValidateInitialized();
            var block = (Relationships.FirstOrDefault(r =>
                r.Type == RelationshipType.Block && r.Member.Id == memberId));
            if (block == null)
                throw new RelationshipNotFoundException(memberId);
            await DeleteRelationshipAsync(block.Id);
        }

        ///<inheritdoc cref="IFriendsService.DeleteRelationshipAsync"/>
        public async Task DeleteRelationshipAsync(string relationshipId)
        {
            ValidateInitialized();
            await m_FriendsApi.DeleteRelationshipAsync(relationshipId);
            var foundRelationship = m_Relationships.FirstOrDefault(r => r.Id == relationshipId);
            if (foundRelationship != null && !m_Relationships.Remove(foundRelationship))
            {
                await InitializeRelationshipsAsync();
            }
        }

        ///<inheritdoc cref="IFriendsService.SetPresenceAvailabilityAsync"/>
        public async Task SetPresenceAvailabilityAsync(Availability availabilityOption)
        {
            ValidateInitialized();
            await m_FriendsApi.SetPresenceAsync(availabilityOption, m_PresenceActivity);
            m_Availability = availabilityOption;
        }

        ///<inheritdoc cref="IFriendsService.SetPresenceActivityAsync"/>
        public async Task SetPresenceActivityAsync<T>(T activity) where T : new()
        {
            ValidateInitialized();
            await m_FriendsApi.SetPresenceAsync(m_Availability, activity);
            m_PresenceActivity = activity;
        }

        ///<inheritdoc cref="IFriendsService.SetPresenceAsync"/>
        public async Task SetPresenceAsync<T>(Availability availabilityOption, T activity)
            where T : new()
        {
            ValidateInitialized();
            await m_FriendsApi.SetPresenceAsync(availabilityOption, activity);
            m_Availability = availabilityOption;
            m_PresenceActivity = activity;
        }

        ///<inheritdoc cref="IMessagingService.MessageAsync"/>
        public async Task MessageAsync<T>(string targetUserId, T message) where T : new()
        {
            ValidateInitialized();
            await m_FriendsApi.MessageAsync(targetUserId, message);
        }

        ///<inheritdoc cref="IFriendsService.ForceRelationshipsRefreshAsync"/>
        public async Task ForceRelationshipsRefreshAsync()
        {
            ValidateInitialized();
            await InitializeRelationshipsAsync();
        }

        internal static IFriendsService CreateFriendsServiceAsync()
        {
            var friendsApi = new WrappedFriendsApi(
                InternalFriendsService.Instance.RelationshipsApi,
                InternalFriendsService.Instance.PresenceApi,
                InternalFriendsService.Instance.NotificationsApi,
                InternalFriendsService.Instance.MessagingApi,
                FriendsService.s_Wire,
                FriendsService.s_Metrics
            );

            return new WrappedFriendsService(friendsApi);
        }

        async Task InitializeRelationshipsAsync()
        {
            var options = new RelationshipOptions { MemberOptions = m_InitializeOptions.MemberOptions };
            var relationships = await m_FriendsApi.GetRelationshipsAsync(options);
            var currentResults = relationships;
            while (currentResults.Count == options.PagingOptions.Limit)
            {
                options.PagingOptions.Offset += options.PagingOptions.Limit;
                currentResults = await m_FriendsApi.GetRelationshipsAsync(options);
                relationships.AddRange(currentResults);
            }

            m_Relationships = relationships;
        }

        async Task InitializeEventSubscriptionAsync()
        {
            try
            {
                var callbacks = new FriendsEventCallbacks();
                callbacks.FriendsEventConnectionStateChanged += (e) =>
                {
                    NotificationsConnectivityChanged?.Invoke(new NotificationStateChangedEvent(e));
                };
                callbacks.RelationshipAdded += (e) =>
                {
                    var addedRelationship = e.Relationship;
                    switch (addedRelationship.Type)
                    {
                        case RelationshipType.FriendRequest:
                            m_Relationships.Add(addedRelationship);
                            break;
                        case RelationshipType.Friend:
                            m_Relationships.Remove(Relationships.FirstOrDefault(r =>
                                r.Type == RelationshipType.FriendRequest &&
                                r.Member.Id == addedRelationship.Member.Id));
                            m_Relationships.Add(addedRelationship);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(addedRelationship.Type.ToString());
                    }

                    RelationshipAdded?.Invoke(e);
                };
                callbacks.RelationshipDeleted += (e) =>
                {
                    var deletedRelationship = e.Relationship;
                    m_Relationships.Remove(Relationships.First(r =>
                        r.Type == deletedRelationship.Type &&
                        r.Member.Id == deletedRelationship.Member.Id));

                    RelationshipDeleted?.Invoke(e);
                };
                callbacks.PresenceUpdated += (e) =>
                {
                    var updatedMemberId = e.ID;
                    var updatedMemberPresence = e.Presence;
                    foreach (var r in Relationships)
                    {
                        if (r.Member.Id == updatedMemberId)
                        {
                            r.Member.Presence = updatedMemberPresence;
                        }
                    }

                    PresenceUpdated?.Invoke(e);
                };
                callbacks.MessageReceived += (e) =>
                {
                    MessageReceived?.Invoke(e);
                };

                m_Events = await m_FriendsApi.SubscribeToEventsAsync(callbacks);
            }
            catch (FriendsServiceException e)
            {
                // Todo gotta implement this with something better
                Debug.Log(
                    "An error occurred while performing the action. Code: " + e.StatusCode + ", Message: " + e.Message);
            }
        }

        async Task<Relationship> AddRelationshipAsync(string memberId, RelationshipType type)
        {
            var relationship = await m_FriendsApi.AddRelationshipAsync(memberId, type, m_InitializeOptions.MemberOptions);
            if (type == RelationshipType.FriendRequest || type == RelationshipType.Friend)
            {
                m_Relationships.Remove(Relationships.FirstOrDefault(r =>
                    r.Type == RelationshipType.FriendRequest &&
                    r.Member.Id == memberId));
            }

            m_Relationships.Add(relationship);
            return relationship;
        }

        void ValidateInitialized([CallerMemberName] string memberName = "")
        {
            if (m_Initialized != true)
                throw new InvalidOperationException($"Unable to call {nameof(FriendsService)}.{memberName} because the Friends Service is not initialized. {nameof(FriendsService)}.{nameof(InitializeAsync)} must be called first.");
        }

        internal void Cleanup()
        {
            if (m_Initialized)
            {
                m_Initialized = false;
                m_CleanupTask = CleanupAsync();
            }
        }

        internal async Task CleanupAsync()
        {
            m_Relationships = new List<Relationship>();
            if (m_Events != null)
                await m_Events.UnsubscribeAsync();
            m_CleanupTask = Task.CompletedTask;
        }
    }
}
