using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Friends.Models;


namespace Unity.Services.Friends.Options
{
    /// <summary>
    /// Defines the options to query the relationship data.
    /// </summary>
    public class RelationshipOptions
    {
        internal PagingOptions PagingOptions { get; set; } = new PagingOptions();
        internal MemberOptions MemberOptions { get; set; } = new MemberOptions();
        internal List<string> RelationshipTypes { get; set; }

        /// <summary>
        /// Defines the paging options for the results
        /// </summary>
        /// <param name="pagingOptions">Options to define the paging of the results</param>
        /// <returns>Itself</returns>
        public RelationshipOptions WithPaging(PagingOptions pagingOptions)
        {
            PagingOptions = pagingOptions;
            return this;
        }

        /// <summary>
        /// Defines whether or not to attach presence data to the member object
        /// </summary>
        /// <param name="withPresence">Whether or not to attach presence data</param>
        /// <returns>The <see cref="RelationshipOptions"/> with the updated presence flag</returns>
        public RelationshipOptions WithMemberPresence(bool withPresence)
        {
            MemberOptions.IncludePresence = withPresence;
            return this;
        }

        /// <summary>
        /// Defines whether or not to attach profile data to the member object
        /// </summary>
        /// <param name="withProfile">whether or not to attach profile data</param>
        /// <returns>The <see cref="RelationshipOptions"/> with the updated profile flag</returns>
        public RelationshipOptions WithMemberProfile(bool withProfile)
        {
            MemberOptions.IncludeProfile = withProfile;
            return this;
        }

        /// <summary>
        /// Defines the types of relationships to return as part of the query
        /// </summary>
        /// <param name="relationshipType">the relationship types to query</param>
        /// <returns>The <see cref="RelationshipOptions"/> with updated types</returns>
        public RelationshipOptions WithTypes(RelationshipType relationshipType)
        {
            if (RelationshipTypes is null)
                RelationshipTypes = new List<string>();

            foreach (RelationshipType enumValue in Enum.GetValues(typeof(RelationshipType)))
            {
                if (relationshipType.HasFlag(enumValue))
                {
                    var internalRelationshipType = EnumParseHelper.ToInternalRelationshipType(enumValue);
                    RelationshipTypes.Add(EnumParseHelper.GetEnumMemberAttrValue(internalRelationshipType));
                }
            }

            if (!RelationshipTypes.Any())
            {
                RelationshipTypes = null;
            }

            return this;
        }
    }
}
