using System;
using Unity.Services.Core;

namespace Unity.Services.Friends.Exceptions
{
    /// <summary>
    /// Represents an exception that occurs when communicating with the Unity Relationships Service.
    /// </summary>
    [Obsolete("RelationshipsServiceException has been changed to FriendsServiceException.", true)]
    public class RelationshipsServiceException : RequestFailedException
    {
        /// <summary>
        /// The reason of the exception.
        /// </summary>
        [Obsolete("Reason has been changed to StatusCode within the FriendsServiceException.", true)]
        public RelationshipErrorCode Reason { get; }

        /// <summary>
        /// Specific error code
        /// </summary>
        public new RelationshipErrorCode ErrorCode { get; }

        /// <summary>
        /// Creates a FriendsServiceException.
        /// </summary>
        /// <param name="reason">The error code or the HTTP Status returned by the service.</param>
        /// <param name="message">The description of the exception.</param>
        /// <param name="innerException">The exception raised by the service, if any.</param>
        /// <param name="errorCode">The error code to be used when raising the issue to the sdk user.</param>
        public RelationshipsServiceException(RelationshipErrorCode reason, string message,
                                             Exception innerException, RelationshipErrorCode errorCode = RelationshipErrorCode.Unknown) : base(
            (int)reason, message, innerException)
        {
            Reason = reason;
            ErrorCode = errorCode;
        }
    }
}
