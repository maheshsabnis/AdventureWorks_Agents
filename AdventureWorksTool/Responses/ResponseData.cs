namespace AdventureWorksTool.Responses
{
    public class ResponseData
    {
        public string Message { get; set; } = string.Empty;

        public IReadOnlyList<Dictionary<string, object?>> Data { get; init; } = [];

        public int Count => Data.Count;
    }
}
