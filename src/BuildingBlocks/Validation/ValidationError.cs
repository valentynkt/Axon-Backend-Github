namespace BuildingBlocks.Validation
{
    public class ValidationError
    {
        public string Field { get; }

        public string Message { get; }

        public ValidationError(string field, string message)
        {
            Field = !string.IsNullOrEmpty(field) ? field : string.Empty;
            Message = message;
        }
    }
}