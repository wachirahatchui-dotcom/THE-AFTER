using System;
using Unity.Services.Authentication;
using Unity.Services.Core.Telemetry.Internal;
using Unity.Services.Wire.Internal;
using InternalFriendsService = Unity.Services.Friends.Internal.Generated.FriendsService;

namespace Unity.Services.Friends
{
    /// <summary>
    /// Provides access to the features of the Friends package.  The IFriendsService Instance property provides
    /// a singleton that can be used to manage create, manage, and update friendships and other relationships.
    /// </summary>
    public static class FriendsService
    {
        static IFriendsService s_Service;
        internal static IWire s_Wire;
        internal static IMetrics s_Metrics;

        /// <summary>
        /// Provides the Friends Service SDK interface for making service API requests.
        /// It is highly recommended to use the ManagedRelationshipService instead due to the sheer amount of convenience methods it has.
        /// In order to access the ManagedRelationshipService object, call ManagedRelationshipService.CreateManagedRelationshipServiceAsync()
        /// </summary>
        public static IFriendsService Instance
        {
            get
            {
                if (s_Service != null)
                    return s_Service;

                if (InternalFriendsService.Instance == null)
                    throw new InvalidOperationException($"Unable to call Instance of {nameof(FriendsService)} because the Friends package is not initialized. {nameof(Core.UnityServices.InitializeAsync)} must be called first.");

                if (AuthenticationService.Instance.PlayerId == null)
                    throw new InvalidOperationException($"Unable to call Instance of {nameof(FriendsService)} because no user is signed in.");

                var wrappedFriendsApi = new WrappedFriendsApi(
                    InternalFriendsService.Instance.RelationshipsApi,
                    InternalFriendsService.Instance.PresenceApi,
                    InternalFriendsService.Instance.NotificationsApi,
                    InternalFriendsService.Instance.MessagingApi,
                    s_Wire,
                    s_Metrics
                );

                s_Service = new WrappedFriendsService(wrappedFriendsApi);

                return s_Service;
            }
        }

        internal static void ClearService()
        {
            (s_Service as WrappedFriendsService)?.Cleanup();
        }
    }
}
