using System.Threading.Tasks;

using Unity.Services.Friends.Exceptions;

namespace Unity.Services.Friends.Notifications
{
    /// <summary>
    /// An interface allowing the subscription and unsubscription to the Friends notification system and the callbacks
    /// invoked when an event occurs on the notification system.
    /// </summary>
    public interface IFriendsEvents
    {
        /// <summary>
        /// The callbacks associated with the Friends events subscription.
        /// </summary>
        FriendsEventCallbacks Callbacks { get; }

        /// <summary>
        /// Subscribes or re-subscribes to the events system.
        /// </summary>
        /// <returns>An awaitable task.</returns>
        /// <exception cref="FriendsServiceException">Represents an exception that occurs when communicating with the Unity Friends service.</exception>
        Task SubscribeAsync();

        /// <summary>
        /// Unsubscribes from the events system.
        /// </summary>
        /// <returns>An awaitable task.</returns>
        /// <exception cref="FriendsServiceException">Represents an exception that occurs when communicating with the Unity Friends service.</exception>
        Task UnsubscribeAsync();
    }
}
