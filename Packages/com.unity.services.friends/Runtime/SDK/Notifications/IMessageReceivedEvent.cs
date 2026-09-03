namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Event triggered when a message is received from another user.
    /// Not intended to be used as a chat.
    /// </summary>
    public interface IMessageReceivedEvent
    {
        /// <summary>
        /// Getter for the identifier of the member who sent the message.
        /// </summary>
        /// <returns>The identifier of the member who sent the message.</returns>
        public string UserId { get; }

        /// <summary>
        /// Getter for the message received.
        /// </summary>
        /// <returns>The message received.</returns>
        public T GetAs<T>() where T : new();
    }
}
