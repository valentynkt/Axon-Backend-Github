namespace Axon.Api.Constants;

/// <summary>
/// Constants for Chat API validation and configuration
/// </summary>
public static class ChatApiConstants
{
    /// <summary>
    /// Valid values for conversation sorting
    /// </summary>
    public static class SortBy
    {
        public const string UpdatedAt = "UpdatedAt";
        public const string CreatedAt = "CreatedAt";
        public const string Title = "Title";

        /// <summary>
        /// All valid sort by values
        /// </summary>
        public static readonly string[] ValidValues = [UpdatedAt, CreatedAt, Title];
    }

    /// <summary>
    /// Valid values for sort direction
    /// </summary>
    public static class SortDirection
    {
        public const string Asc = "Asc";
        public const string Desc = "Desc";

        /// <summary>
        /// All valid sort direction values
        /// </summary>
        public static readonly string[] ValidValues = [Asc, Desc];
    }
}