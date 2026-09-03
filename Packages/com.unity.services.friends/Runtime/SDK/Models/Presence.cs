using System;
using Unity.Services.Friends.Internal.Generated.Http;

namespace Unity.Services.Friends.Models
{
    /// <summary>
    /// Presence model
    /// </summary>
    public class Presence
    {
        /// <summary>
        /// The availability of the user
        /// </summary>
        public Availability Availability { get; internal set; }

        /// <summary>
        /// The last seen time of the user in UTC
        /// </summary>
        public DateTime LastSeen { get; internal set; }

        internal IDeserializable Activity { get; set; }

        /// <summary>
        /// Getter for the current user's activity
        /// </summary>
        /// <returns>The activity of the user</returns>
        public T GetActivity<T>() where T : new()
        {
            return Activity.GetAs<T>(new DeserializationSettings(){MissingMemberHandling = MissingMemberHandling.Ignore});
        }

        internal void SetActivity(IDeserializable activity)
        {
            Activity = activity;
        }
    }
}
