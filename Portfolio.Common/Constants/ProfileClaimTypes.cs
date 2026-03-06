namespace Portfolio.Common.Constants
{
    /// <summary>
    /// Well-known claim types stored in the UserClaims table.
    /// </summary>
    public static class ProfileClaimTypes
    {
        public const string GoogleId = "google_id";
        public const string Email = "email";
        public const string Name = "name";
        public const string Picture = "picture";

        /// <summary>
        /// User-editable display name that overrides the Google-sourced name.
        /// </summary>
        public const string DisplayName = "display_name";
    }
}