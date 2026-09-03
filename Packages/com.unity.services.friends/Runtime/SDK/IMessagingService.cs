using System.Threading.Tasks;

namespace Unity.Services.Friends
{
    /// <summary>
    /// IMessagingService allows users to send a message to other users.
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        /// Sends a message from the current user to the target user. To ease deserialization of sent message
        /// on the receiving client, the message must have an empty constructor.
        /// The maximum size of the message payload is 10 kilobytes.
        /// </summary>
        /// <param name="targetUserId">The ID of the user to target with the message. Must be friends with the current user.</param>
        /// <param name="message">The message to send. It must be serializable (and deserializable) as JSON</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="System.InvalidOperationException">Represents an error that occurs when the service has not been initialized.</exception>
        /// <exception cref="System.ArgumentException">Represents an error that occur when an argument is incorrectly set up.</exception>
        /// <exception cref="Unity.Services.Friends.Exceptions.FriendsServiceException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task MessageAsync<T>(string targetUserId, T message) where T : new();
    }
}
