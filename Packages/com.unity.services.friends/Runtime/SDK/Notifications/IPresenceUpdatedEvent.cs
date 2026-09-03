using Unity.Services.Friends.Models;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Event triggered when another user that has a relationship with the current user updates their presence.
    /// </summary>
    public interface IPresenceUpdatedEvent
    {
        /// <summary>
        /// Getter for the identifier of the member whose presence got updated.
        /// </summary>
        /// <returns>The identifier of the member whose presence got updated.</returns>
        public string ID { get; }

        /// <summary>
        /// Getter for the presence of the member whose presence got updated.
        /// </summary>
        /// <returns>The presence of the member whose presence got updated.</returns>
        public Presence Presence { get; }
    }
}
