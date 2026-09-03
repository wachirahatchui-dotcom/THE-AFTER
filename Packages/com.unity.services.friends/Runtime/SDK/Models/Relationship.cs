namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// The representation of a relationship between members
    /// </summary>
    public class Relationship
    {
        /// <summary>
        /// The id of the relationship
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// The type of relationship
        /// </summary>
        public RelationshipType Type { get; internal set; }

        /// <summary>
        /// The member with whom the current user has the relationship
        /// </summary>
        public Member Member { get; internal set; }
    }
}
