using System.Net.Http.Json;
using System.Text.Json;
using AdventureWorksClient.Contracts;

namespace AdventureWorksClient.Services;

public sealed class AgentApiClient(HttpClient httpClient)
{
    // Send the prompt and previous messages to the API, read its reply, and report HTTP errors.
    public async Task<AgentResponse> AskAsync(string prompt, List<AgentHistoryMessage> history, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/agent", new { prompt, history }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = $"The API returned HTTP {(int)response.StatusCode}. Please try again.";
            if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
            {
                try
                {
                    var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && problem?.Errors?.Count > 0)
                    {
                        message = string.Join(" ", problem.Errors.Values.SelectMany(errors => errors));
                    }
                    else if (!string.IsNullOrWhiteSpace(problem?.Title))
                    {
                        message = $"{message} {problem.Title}";
                    }
                }
                catch (JsonException)
                {
                    // Keep the HTTP status when the server returns a malformed problem response.
                }
            }

            throw new HttpRequestException(message, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<AgentResponse>(cancellationToken)
            ?? throw new JsonException("The API returned an empty response.");
    }

    private sealed class ApiProblem
    {
        public string? Title { get; init; }
        public Dictionary<string, string[]>? Errors { get; init; }
    }
}
