namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// The representation of a user in a relationship
    /// </summary>
    public class Member
    {
        /// <summary>
        /// The ID of the member
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// The member's role
        /// </summary>
        public MemberRole Role { get; internal set; }

        /// <summary>
        /// The presence data of the member
        /// </summary>
        public Presence Presence { get; internal set; }

        /// <summary>
        /// The profile data of the member
        /// </summary>
        public Profile Profile { get; internal set; }
    }
}
