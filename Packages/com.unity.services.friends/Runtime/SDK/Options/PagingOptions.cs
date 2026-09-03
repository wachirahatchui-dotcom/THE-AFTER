namespace Unity.Services.Friends.Options
{
    /// <summary>
    /// Query parameters for pagination service requests.
    /// </summary>
    public class PagingOptions
    {
        /// <summary>
        /// Maximum records to return per page. The default value is 50.
        /// </summary>
        public int Limit { get; set; } = 50;

        /// <summary>
        /// Maximum offset for pagination. The default value is 0.
        /// </summary>
        public int Offset { get; set; } = 0;
    }
}
