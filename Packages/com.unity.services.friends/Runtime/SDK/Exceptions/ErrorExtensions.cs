using System;

namespace Unity.Services.Friends.Exceptions
{
    static class ErrorExtensions
    {
        internal static FriendsErrorCode FromCode(int codeValue)
        {
            if (Enum.IsDefined(typeof(FriendsErrorCode), codeValue))
            {
                return (FriendsErrorCode)codeValue;
            }

            return FriendsErrorCode.Unknown;
        }
    }
}
