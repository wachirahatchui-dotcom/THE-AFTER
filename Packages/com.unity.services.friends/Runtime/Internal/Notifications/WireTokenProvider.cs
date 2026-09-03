using System.Threading.Tasks;
using Unity.Services.Friends.Internal.Generated;
using Unity.Services.Wire.Internal;

namespace Unity.Services.Friends.Internal.Notifications
{
    internal class WireTokenProvider : IChannelTokenProvider
    {
        readonly IFriendsServiceInternal m_friendService;

        public WireTokenProvider(IFriendsServiceInternal friendService)
        {
            m_friendService = friendService;
        }

        public Task<ChannelToken> GetTokenAsync()
        {
            return m_friendService.GetNotificationsAuthAsync();
        }
    }
}
