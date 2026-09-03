using System;
using System.Net;
using Unity.Services.Core;

namespace Unity.Services.Friends.Exceptions
{
    /// <summary>
    /// Represents an exception that occurs when communicating with the Unity Friends service.
    /// </summary>
    public class FriendsServiceException : RequestFailedException
    {
        /// <summary>
        /// The HTTP status code of the exception
        /// </summary>
        public HttpStatusCode StatusCode { get; }


        /// <summary>
        /// The reason of the exception.
        /// </summary>
        [Obsolete("Reason has been changed to StatusCode.", true)]
        public RelationshipErrorCode Reason { get; }

        /// <summary>
        /// Specific error code
        /// </summary>
        public new FriendsErrorCode ErrorCode { get; }

        /// <summary>
        /// Creates a FriendsServiceException.
        /// </summary>
        /// <param name="statusCode">The HTTP Status returned by the service.</param>
        /// <param name="message">The description of the exception.</param>
        /// <param name="innerException">The exception raised by the service, if any.</param>
        /// <param name="errorCode">The error code to be used when raising the issue to the SDK user.</param>
        public FriendsServiceException(HttpStatusCode statusCode, string message,
                                       Exception innerException, FriendsErrorCode errorCode = FriendsErrorCode.Unknown) : base(
            (int)statusCode, message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
}
