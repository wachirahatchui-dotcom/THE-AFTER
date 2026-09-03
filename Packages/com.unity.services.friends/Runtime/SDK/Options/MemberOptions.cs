namespace Unity.Services.Friends.Options
{
    /// <summary>
    /// Defines options to select the desired data when retrieving member information.
    /// </summary>
    class MemberOptions
    {
        internal bool IncludePresence { get; set; } = true;
        internal bool IncludeProfile { get; set; } = true;
    }
}
