using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core.Telemetry.Internal;
using System.Net;

// Friends
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Http;
using Unity.Services.Friends.Internal;
using Unity.Services.Friends.Internal.Generated;
using Unity.Services.Friends.Internal.Generated.Apis.Messaging;
using Unity.Services.Friends.Internal.Generated.Apis.Presence;
using Unity.Services.Friends.Internal.Generated.Apis.Notifications;
using Unity.Services.Friends.Internal.Generated.Apis.Relationships;
using Unity.Services.Friends.Internal.Generated.Models;
using Unity.Services.Friends.Internal.Generated.Relationships;
using Unity.Services.Friends.Internal.Generated.Http;
using Unity.Services.Friends.Internal.Generated.Notifications;
using Unity.Services.Friends.Internal.Generated.Presence;
using Unity.Services.Friends.Internal.Notifications;
using Debug = Unity.Services.Friends.Logger;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using Unity.Services.Wire.Internal;
using DeleteRelationshipRequest = Unity.Services.Friends.Internal.Generated.Relationships.DeleteRelationshipRequest;
using Member = Unity.Services.Friends.Models.Member;
using Relationship = Unity.Services.Friends.Models.Relationship;
using RelationshipType = Unity.Services.Friends.Models.RelationshipType;
using InternalRelationshipType = Unity.Services.Friends.Internal.Generated.Models.RelationshipType;
using AddRelationshipRequestParam = Unity.Services.Friends.Internal.Generated.Models.AddRelationshipRequest;
using FriendsServiceException = Unity.Services.Friends.Exceptions.FriendsServiceException;
using InternalRelationship = Unity.Services.Friends.Internal.Generated.Models.Relationship;

namespace Unity.Services.Friends
{
    /// <summary>
    /// The Friends service enables clients to use friends features within their project.
    /// </summary>
    sealed class WrappedFriendsApi : IFriendsApi, IFriendsServiceInternal
    {
        /// <summary>
        /// API object to access relationships functionalities
        /// </summary>
        readonly IRelationshipsApiClient m_RelationshipsApi;

        /// <summary>
        /// API object to access presence functionalities
        /// </summary>
        readonly IPresenceApiClient m_PresenceApi;

        /// <summary>
        /// API object to access notification functionalities
        /// </summary>
        readonly INotificationsApiClient m_NotificationApi;

        /// <summary>
        /// API object to access messaging functionalities
        /// </summary>
        IMessagingApiClient m_MessagingApi;

        /// <summary>
        /// The Wire component for the service.
        /// </summary>
        internal IWire m_Wire;

        /// <summary>
        /// Configuration to access the Friends APIs
        /// </summary>
        internal static Configuration s_Configuration =
            new Configuration("https://social.services.api.unity.com", 10, 4, null);

        /// <summary>
        /// Error when the ID argument is invalid.
        /// </summary>
        const string k_IdValidationError = "ID can't be null, empty or contain white spaces";

        /// <summary>
        /// Error when the ID argument is invalid.
        /// </summary>
        const string k_NameValidationError = "Name can't be null, empty or contain white spaces";

        /// <summary>
        /// Task scheduler for the internal Dispose when setting player's presence to offline.
        /// </summary>
        readonly TaskScheduler m_taskScheduler;

        /// <summary>
        /// The telemetry scope factory used to track metrics for API requests.
        /// </summary>
        readonly ApiTelemetryScopeFactory m_TelemetryScopeFactory;

        /// <summary>
        /// Constructor of the WrappedRelationshipsService following the IFriendsApi rules.
        /// </summary>
        /// <param name="relationshipsApi">API to access relationships functionalities</param>
        /// <param name="presenceApi">API to access presence functionalities</param>
        /// <param name="notificationApi">API to access notification functionalities</param>
        /// <param name="messagingApi">API to access the messaging functionalities</param>
        /// <param name="wire">The Wire component for the service</param>
        /// <param name="metrics">The metrics component for the service</param>
        internal WrappedFriendsApi(
            IRelationshipsApiClient relationshipsApi,
            IPresenceApiClient presenceApi,
            INotificationsApiClient notificationApi,
            IMessagingApiClient messagingApi,
            IWire wire,
            IMetrics metrics
        )
        {
            m_MessagingApi = messagingApi;
            m_RelationshipsApi = relationshipsApi;
            m_PresenceApi = presenceApi;
            m_NotificationApi = notificationApi;
            m_Wire = wire;
            m_taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            m_TelemetryScopeFactory = new ApiTelemetryScopeFactory(metrics);
        }

        /// <summary>
        /// Finalizer of the WrappedFriendsService class.
        /// Invoked when the class instance is collected by the garbage collector.
        /// </summary>
        ~WrappedFriendsApi()
        {
            Dispose(false);
        }

        /// <summary>
        /// Performs Friends-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// BlankActivity is used to send an Activity that contains no metadata elements.
        /// </summary>
        internal class BlankActivity
        {
        }

        /// <summary>
        /// Schedule a task setting the presence of the currently logged in player to the OFFLINE availability.
        /// </summary>
        /// /// <param name="disposing">Indicates if the call is from a Dispose method or from a finalizer.</param>
        void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Task.Run(async() =>
            {
                try
                {
                    await SetPresenceAsync(Availability.Offline, new BlankActivity());
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            });
        }

        ///<inheritdoc cref="IFriendsApi.GetRelationshipsAsync"/>
        public async Task<List<Relationship>> GetRelationshipsAsync(RelationshipOptions relationshipOptions)
        {
            // ToDo add possible validation of relationshipOptions
            var response = await TryCatchRequest(RelationshipsApiNames.GetRelationships,
                m_RelationshipsApi.GetRelationshipsAsync,
                new GetRelationshipsRequest(relationshipOptions.PagingOptions.Limit,
                    relationshipOptions.PagingOptions.Offset, relationshipOptions.MemberOptions.IncludePresence,
                    relationshipOptions.MemberOptions.IncludeProfile, relationshipOptions.RelationshipTypes)
            );

            return new List<Relationship>(response.Result.Select(r =>
                TransformResponseRelationship(r, relationshipOptions.MemberOptions)));
        }

        ///<inheritdoc cref="IFriendsApi.AddRelationshipAsync"/>
        public async Task<Relationship> AddRelationshipAsync(string memberId, RelationshipType relationshipType,
            MemberOptions memberOptions)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException(k_IdValidationError, nameof(memberId));
            }

            // ToDo possible add validation for MemberOptions

            var memberIdentity = new MemberIdentity(id: memberId, role: Role.TARGET);
            var relationshipRequestParam = new AddRelationshipRequestParam(
                EnumParseHelper.ToInternalRelationshipType(relationshipType),
                new List<MemberIdentity> { memberIdentity });
            var response = await TryCatchRequest(RelationshipsApiNames.CreateRelationship,
                m_RelationshipsApi.CreateRelationshipAsync,
                new CreateRelationshipRequest(memberOptions.IncludePresence, memberOptions.IncludeProfile,
                    relationshipRequestParam));

            return TransformResponseRelationship(response.Result, memberOptions);
        }

        ///<inheritdoc cref="IFriendsApi.AddFriendByNameAsync"/>
        public async Task<Relationship> AddFriendByNameAsync(string name, MemberOptions memberOptions)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(k_NameValidationError, nameof(name));
            }

            // ToDo possible add validation for MemberOptions

            var memberIdentity = new MemberIdentity(profileName: name, role: Role.TARGET);
            var relationshipRequestParam = new AddRelationshipRequestParam(
                EnumParseHelper.ToInternalRelationshipType(RelationshipType.Friend),
                new List<MemberIdentity> { memberIdentity });
            var response = await TryCatchRequest(RelationshipsApiNames.CreateRelationship,
                m_RelationshipsApi.CreateRelationshipAsync,
                new CreateRelationshipRequest(memberOptions.IncludePresence, memberOptions.IncludeProfile,
                    relationshipRequestParam));

            return TransformResponseRelationship(response.Result, memberOptions);
        }

        ///<inheritdoc cref="IFriendsApi.DeleteRelationshipAsync"/>
        public async Task DeleteRelationshipAsync(string relationshipId)
        {
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                throw new ArgumentException(k_IdValidationError, nameof(relationshipId));
            }

            await TryCatchRequest(RelationshipsApiNames.DeleteRelationship, async(r, c) =>
            {
                var result = await m_RelationshipsApi.DeleteRelationshipAsync(r, c);
                // Generated SDK code does not return a Response<T> object, adding one in wrapper with default values to match required signature
                return new Response<bool>(
                    new HttpClientResponse(null, result.Status, false, false, null, string.Empty),
                    true
                );
            }, new DeleteRelationshipRequest(relationshipId));
        }

        ///<inheritdoc cref="IFriendsApi.SetPresenceAsync"/>
        public async Task SetPresenceAsync<T>(Availability availabilityOption, T activity)
            where T : new()
        {
            await TryCatchRequest(PresenceApiNames.SetPresence,
                m_PresenceApi.SetPresenceAsync,
                new SetPresenceRequest(
                    new PresenceRequest(
                        EnumParseHelper.ToInternalPresenceAvailabilityOptions(availabilityOption),
                        activity)));
        }

        ///<inheritdoc cref="IFriendsServiceInternal.GetNotificationsAuthAsync"/>
        public async Task<ChannelToken> GetNotificationsAuthAsync()
        {
            var response = await TryCatchRequest(NotificationsApiNames.GetNotificationsAuth,
                m_NotificationApi.GetNotificationsAuthAsync,
                new GetNotificationsAuthRequest());

            return new ChannelToken { ChannelName = response.Result.Channel, Token = response.Result.Token };
        }

        ///<inheritdoc cref="IFriendsApi.SubscribeToEventsAsync"/>
        public async Task<IFriendsEvents> SubscribeToEventsAsync(FriendsEventCallbacks callbacks)
        {
            var subscription = m_Wire.CreateChannel(new WireTokenProvider(this));
            var relationshipsChannel = new FriendsChannel(subscription, callbacks);
            await relationshipsChannel.SubscribeAsync();

            return relationshipsChannel;
        }

        public async Task MessageAsync<T>(string targetUserId, T message) where T : new()
        {
            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                throw new ArgumentException(k_IdValidationError, nameof(targetUserId));
            }

            await TryCatchRequest(MessagingApiNames.Message, async(r, c) =>
            {
                var result = await m_MessagingApi.MessageAsync(r, c);
                return new Response<bool>(
                    new HttpClientResponse(null, result.Status, false, false, null, string.Empty),
                    true
                );
            },
                new Unity.Services.Friends.Internal.Generated.Messaging.MessageRequest(
                    new MessageRequest(targetUserId, message)));
        }

        // Helper method to transform internal relationship responses
        internal static Relationship TransformResponseRelationship(InternalRelationship internalRelationship,
            MemberOptions memberOptions)
        {
            var internalMember = internalRelationship.Members.First();
            var relationship = new Relationship
            {
                Id = internalRelationship.Id,
                Type = EnumParseHelper.FromInternalRelationshipType(internalRelationship.Type),
                Member = new Member
                {
                    Id = internalRelationship.Members.First().Id,
                    Role = EnumParseHelper.FromInternalMemberRole(internalMember.Role)
                }
            };

            if (memberOptions.IncludePresence && internalMember.Presence != null)
            {
                var presence = new Presence()
                {
                    Availability =
                        EnumParseHelper.FromInternalPresenceAvailabilityOptions(internalMember.Presence.Availability),
                    LastSeen = internalMember.Presence.LastSeen
                };
                presence.SetActivity(internalMember.Presence.Activity);
                relationship.Member.Presence = presence;
            }

            if (memberOptions.IncludeProfile && internalMember.Profile != null)
            {
                relationship.Member.Profile =
                    new Profile { Name = internalMember.Profile.Name };
            }

            return relationship;
        }

        // Helper function to reduce code duplication of try-catch
        async Task<Response<TReturn>> TryCatchRequest<TRequest, TReturn>(
            string api, Func<TRequest, Configuration, Task<Response<TReturn>>> func, TRequest request)
        {
            Response<TReturn> response = null;
            try
            {
                using (m_TelemetryScopeFactory.Instrument(api))
                {
                    response = await func(request, s_Configuration);
                }
            }
            catch (HttpException<Error> he)
            {
                ResolveErrorWrapping((HttpStatusCode)he.ActualError.Status, he);
            }
            catch (HttpException he)
            {
                ResolveErrorWrapping((HttpStatusCode)he.Response.StatusCode, he);
            }
            catch (Exception e)
            {
                //Pass error code that will throw default label, provide exception object for stack trace.
                ResolveErrorWrapping(HttpStatusCode.InternalServerError, e);
            }

            return response;
        }

        // Helper function to resolve the new wrapped error/exception based on input parameter
        static void ResolveErrorWrapping(HttpStatusCode statusCode, Exception exception = null)
        {
            if (statusCode == HttpStatusCode.InternalServerError)
            {
                throw new FriendsServiceException(HttpStatusCode.InternalServerError, "Something went wrong.", exception);
            }

            if ((int)statusCode >= 400 && (int)statusCode < 500 &&
                exception is HttpException<Error> fourHundredException)
            {
                throw new FriendsServiceException(statusCode,
                    (fourHundredException.ActualError?.Details?.FirstOrDefault()?.Message) ?? "", exception,
                    ErrorExtensions.FromCode(fourHundredException.ActualError.Code));
            }

            if (exception is HttpException<Error> apiException)
            {
                throw new FriendsServiceException((HttpStatusCode)apiException.ActualError.Status, apiException.ActualError.Title, apiException);
            }

            throw new FriendsServiceException(statusCode, exception != null ? exception.Message : "", exception);
        }
    }
}
