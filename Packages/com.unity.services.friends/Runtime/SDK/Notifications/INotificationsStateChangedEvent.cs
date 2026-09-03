namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Event triggered when the state has changed for the friends notification system.
    /// </summary>
    public interface INotificationsStateChangedEvent

    {

        /// <summary>
        /// Getter for the current state of friends notification system.
        /// </summary>
        /// <returns>The new state of the notification system.</returns>
        public FriendsEventConnectionState State { get; }
    }
}
