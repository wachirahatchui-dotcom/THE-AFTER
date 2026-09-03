using System;

namespace Unity.Services.Friends.Exceptions
{
    /// <summary>
    /// Enumerates the known error causes when communicating with the Relationships Service.
    /// N.B. Error code range for this service: 24000-24999
    /// </summary>
    [Obsolete("RelationshipErrorCode has been changed to FriendsErrorCode. (UnityUpgradable) -> FriendsErrorCode", true)]
    public enum RelationshipErrorCode
    {
        /// <summary>
        /// Error unknown
        /// </summary>
        Unknown = 0,

        // Service error codes
        /// <summary>
        /// Could not get user id from header
        /// </summary>
        GetUserFromHeader =  24000,

        /// <summary>
        /// Could not get project id from header
        /// </summary>
        GetProjectIDFromHeader = 24001,

        /// <summary>
        /// Could not get environment id from header
        /// </summary>
        GetEnvironmentIDFromHeader = 24002,

        /// <summary>
        /// The relationship already exists
        /// </summary>
        RelationshipAlreadyExists = 24003,

        /// <summary>
        /// Error parsing pagination limit, it must be a positive integer
        /// </summary>
        RetrievePaginationLimit = 24004,

        /// <summary>
        /// Error pagination limit must be a positive integer
        /// </summary>
        NegativePaginationLimit = 24005,

        /// <summary>
        /// Error parsing pagination offset, it must be a positive integer
        /// </summary>
        RetrievePaginationOffset = 24006,

        /// <summary>
        /// Error pagination offset must be a positive integer
        /// </summary>
        NegativePaginationOffset = 24007,

        /// <summary>
        /// Error user cannot target self
        /// </summary>
        UserTargetingSelf = 24008,

        /// <summary>
        /// Error cannot perform action when blocked
        /// </summary>
        ActionUnauthorizedWhenBlocked = 20011,

        /// <summary>
        /// Error friendship already exists
        /// </summary>
        FriendshipAlreadyExists = 24012,

        /// <summary>
        /// Error friendship does not exist
        /// </summary>
        FriendshipDoesNotExist = 24013,

        /// <summary>
        /// Error swagger validation
        /// </summary>
        SwaggerValidation = 24016,

        /// <summary>
        /// Error invalid target user format
        /// </summary>
        GetTargetUserFormat = 24019,

        /// <summary>
        /// Error pagination limit must be a positive integer
        /// </summary>
        DecodePresenceSchema = 24020,

        /// <summary>
        /// Error availability has to be an accepted string
        /// </summary>
        InvalidAvailability = 24021,

        /// <summary>
        /// Error max payload size exceeded
        /// </summary>
        MaxPayloadSize = 24022,

        /// <summary>
        /// Error presence flag should be a boolean
        /// </summary>
        RetrievePresenceFlag = 24023,

        /// <summary>
        /// Error project should be enabled un Udash
        /// </summary>
        ProjectNotEnabled = 24024,

        /// <summary>
        /// Error validating JWS
        /// </summary>
        ValidatingJws = 24025,

        /// <summary>
        /// Error decoding player IDs
        /// </summary>
        DecodePlayerIds = 24026,

        /// <summary>
        /// Error profiles flag should be a boolean
        /// </summary>
        RetrieveSocialProfilesFlag = 24027,

        /// <summary>
        /// Error unknown flag format
        /// </summary>
        UnknownFlag = 24028,

        /// <summary>
        /// Error decoding the message schema
        /// </summary>
        DecodeMessageSchema = 24031,

        /// <summary>
        /// Error multiple members with the same user ID
        /// </summary>
        DuplicateMember = 24032,

        /// <summary>
        /// Error creating relationship with the calling user as the target
        /// </summary>
        InvalidCreateTarget = 24033,

        /// <summary>
        /// Error creating relationship with a different user than the signed in one as the source
        /// </summary>
        InvalidCreateSource = 24034,

        /// <summary>
        /// Error calling user should be included as a member in the relationship
        /// </summary>
        InvalidCreateMissingCaller = 24035,

        /// <summary>
        /// Error decoding x-user header
        /// </summary>
        DecodeXUserHeader = 24036,

        /// <summary>
        /// Error unknown or invalid x-user-type
        /// </summary>
        GetUserTypeFromHeader = 24037,

        /// <summary>
        /// Error invalid session ID from x-user header
        /// </summary>
        GetSessionIDFromHeader = 24039,

        /// <summary>
        /// Error too many users specified as target
        /// </summary>
        InvalidCreateTooManyMembers = 24040,

        // 24400 - 24499
        /// <summary>
        /// Error code representing HTTP Status Code of 400 for the Relationships Service.
        /// The Relationships service cannot process the request because it is invalid.
        /// </summary>
        BadRequest = 24400,

        // SDK specific error codes 24600-24699
        /// <summary>
        /// Error occured with the connection to the notification system.
        /// </summary>
        NotificationConnectionError = 24600,

        /// <summary>
        /// An unknown notification was received from the notification system.
        /// </summary>
        NotificationUnknown = 24601,

        /// <summary>
        /// NetworkError is returned when the UnityWebRequest failed with this flag set. See the exception stack trace when this reason is provided for context.
        /// </summary>
        NetworkError = 24602,
    }
}
