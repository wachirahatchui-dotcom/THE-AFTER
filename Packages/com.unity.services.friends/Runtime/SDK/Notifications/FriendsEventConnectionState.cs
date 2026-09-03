namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// An enum describing the current state of a Relationships Event subscription's connection status.
    /// </summary>
    public enum FriendsEventConnectionState
    {
        /// <summary>
        /// The friends event subscription has reached an unknown state.
        /// </summary>
        Unknown,

        /// <summary>
        /// The friends event subscription is currently unsubscribed.
        /// </summary>
        Unsubscribed,

        /// <summary>
        /// The friends event subscription is currently trying to connect to the service.
        /// </summary>
        Subscribing,

        /// <summary>
        /// The friends event subscription is currently connected, and ready to receive notifications.
        /// </summary>
        Subscribed,

        /// <summary>
        /// The friends event subscription is currently connected, but for some reason is having trouble receiving notifications.
        /// </summary>
        Unsynced,

        /// <summary>
        /// The friends event subscription is currently in an error state, and won't recover on its own.
        /// </summary>
        Error,
    }
}
