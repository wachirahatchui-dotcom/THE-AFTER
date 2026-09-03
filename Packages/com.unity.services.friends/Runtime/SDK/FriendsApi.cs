using System;
using InternalFriendsService = Unity.Services.Friends.Internal.Generated.FriendsService;
using Unity.Services.Core.Telemetry.Internal;
using Unity.Services.Wire.Internal;

namespace Unity.Services.Friends
{
    /// <summary>
    /// Provides access to the raw Friends service API. The IFriendsApi Instance property provides a singleton
    /// that can be used to make direct requests to the Friends service to create, manage, and update relationships.
    /// </summary>
    static class FriendsApi
    {
        static IFriendsApi s_Service;

        /// <summary>
        /// Enabling the subscription to a Wire channel.
        /// </summary>
        internal static IWire s_Wire;

        /// <summary>
        /// The metrics component for the service.
        /// </summary>
        internal static IMetrics s_Metrics;

        /// <summary>
        /// Provides the Friends Service SDK interface for making service API requests.
        /// It is highly recommended to use the FriendsService instead due to the sheer amount of convenience methods it has.
        /// </summary>
        public static IFriendsApi Instance
        {
            get
            {
                if (s_Service != null)
                    return s_Service;

                if (InternalFriendsService.Instance == null)
                    throw new InvalidOperationException($"Unable to call Instance of {nameof(FriendsApi)} because the Friends package is not initialized. {nameof(Core.UnityServices.InitializeAsync)} must be called first.");

                s_Service = new WrappedFriendsApi(
                    InternalFriendsService.Instance.RelationshipsApi,
                    InternalFriendsService.Instance.PresenceApi,
                    InternalFriendsService.Instance.NotificationsApi,
                    InternalFriendsService.Instance.MessagingApi,
                    s_Wire,
                    s_Metrics
                );

                return s_Service;
            }
        }
    }
}
