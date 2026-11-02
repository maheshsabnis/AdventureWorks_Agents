using System.Text.Json;

namespace AdventureWorksClient.Contracts;

public class AgentHistoryMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class AgentResponse
{
    public string Message { get; init; } = string.Empty;
    public List<AgentQueryResult> Results { get; init; } = [];
}

public sealed class AgentQueryResult
{
    public string Tool { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Count { get; init; }
    public List<Dictionary<string, JsonElement>> Data { get; init; } = [];
}
