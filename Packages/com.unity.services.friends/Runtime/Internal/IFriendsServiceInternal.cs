using Unity.Services.Core;
using System.Threading.Tasks;
using Unity.Services.Wire.Internal;

namespace Unity.Services.Friends.Internal
{
    internal interface IFriendsServiceInternal
    {
        /// <summary>
        /// Async Operation.
        /// Retrieves both a subscription token and a channel name that the user may use to subscribe to a wire channel.
        /// </summary>
        /// <returns>Awaitable task</returns>
        /// <exception cref="RequestFailedException">An exception containing the HttpClientResponse with headers, response code, and string of error.</exception>
        Task<ChannelToken> GetNotificationsAuthAsync();
    }
}
