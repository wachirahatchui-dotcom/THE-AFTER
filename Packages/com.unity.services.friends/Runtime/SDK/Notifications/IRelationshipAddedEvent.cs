using Unity.Services.Friends.Models;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Event triggered when another user creates a relationship targeting the current user.
    /// </summary>
    public interface IRelationshipAddedEvent
    {
        /// <summary>
        /// Getter for the relationship that was added.
        /// </summary>
        /// <returns>The relationship added.</returns>
        public Relationship Relationship { get; }
    }
}
