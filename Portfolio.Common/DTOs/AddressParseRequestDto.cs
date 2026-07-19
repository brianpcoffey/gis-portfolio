namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request body containing a free-form address string to be parsed into components.
    /// </summary>
    public class AddressParseRequestDto
    {
        /// <summary>Unparsed, free-form address text as entered by the user.</summary>
        public string RawAddress { get; set; } = string.Empty;
    }
}
