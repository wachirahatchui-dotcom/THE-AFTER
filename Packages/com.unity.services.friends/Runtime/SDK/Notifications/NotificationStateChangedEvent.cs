namespace Unity.Services.Friends.Notifications
{
    ///<inheritdoc cref="INotificationsStateChangedEvent"/>
    internal class NotificationStateChangedEvent: INotificationsStateChangedEvent
    {
        internal NotificationStateChangedEvent(FriendsEventConnectionState state)
        {
            State = state;
        }

        public FriendsEventConnectionState State { get; }
    }
}
