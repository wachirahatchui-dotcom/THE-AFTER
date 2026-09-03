using Unity.Services.Friends.Models;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// Event triggered when another user deletes a relationship targeting the current user.
    /// </summary>
    public interface IRelationshipDeletedEvent
    {
        /// <summary>
        /// Getter for the relationship that was deleted.
        /// </summary>
        /// <returns>The relationship deleted.</returns>
        public Relationship Relationship { get; }
    }
}
