# AdventureWorks Blazor WebAssembly client

A standalone .NET 10 browser client for `POST /api/agent`. `Components/AgentChat.razor` provides the chat interface; `Components/QueryResultTable.razor` renders each successful tool's actual database rows with 25-row pagination.

## Run in Visual Studio

1. Configure the API's existing `ConnectionStrings:DefaultConnection`, `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, and `AzureOpenAI:DeploymentName`. Keep credentials in server-side user secrets or environment variables.
2. Trust the ASP.NET Core development HTTPS certificate (`dotnet dev-certs https --trust`) if needed.
3. In solution startup settings, choose multiple startup projects and set both `AdventureWorksTool` and `AdventureWorksClient` to **Start**. Select the **https** launch profile for each. Reload the solution if the newly added project is not yet visible.
4. Open `https://localhost:7248`. The API listens at `https://localhost:7113`; Swagger remains available in Development at `/swagger`.

Alternatively, run these in separate terminals from the solution root:

- `dotnet run --project AdventureWorksTool --launch-profile https`
- `dotnet run --project AdventureWorksClient --launch-profile https`

The client HTTP profile uses `http://localhost:5248`, but still calls the HTTPS API by default. Always call the API's HTTPS address directly; HTTP-to-HTTPS redirects can break browser preflight requests.

## Configuration and security

- Client: set `ApiBaseUrl` in `wwwroot/appsettings.json` to the API base address. Every file under `wwwroot`, including configuration, is public. Never put Azure API keys or connection strings here.
- API: configure `Cors:AllowedOrigins` with exact browser origins (scheme, hostname, and port, without a trailing slash). Development allows `https://localhost:7248` and `http://localhost:5248`.
- For production, set the API origins through production configuration or environment variables such as `Cors__AllowedOrigins__0`. With no origins configured, cross-origin browser access is denied.
- The CORS policy permits POST and Content-Type for the agent endpoint; it does not permit wildcard origins or cross-origin credentials.
- CORS is not authentication. Protect the API with appropriate authentication, authorization, and rate limits before exposing business data publicly.
- Publish the client with `dotnet publish AdventureWorksClient -c Release` and host the published `wwwroot` with an HTTPS static-file host. Configure the public API URL before deployment.

## Behavior

- Prompts are trimmed and limited to 8,000 characters, matching the API.
- Successful user/agent text exchanges are kept in browser memory and sent as `history` with the next prompt. Reload or **Clear chat** resets this context. Failed/canceled calls and query rows are not sent as history.
- The agent asks one clarification question at a time for missing report, territory, or date choices. For example: ask “Show revenue by region”, answer “Northwest”, then “all available dates”. A complete request runs without redundant questions.
- History is limited to 20 prior messages, 8,000 characters per message, and 40,000 history characters. If the limit is reached, clear the chat rather than silently dropping earlier details. There is no shared server-side conversation store.
- Each reply shows the agent message followed by every successful query result, including tool name, query status, row count, and a table with dynamic columns.
- Tables use `results[].data` directly. They preserve numbers as returned, show null values explicitly, and render all content as escaped text rather than HTML.
- The model still receives status messages, not the full database rows; it does not analyze those rows in its response.
- Pagination covers returned rows only; it does not fetch extra database pages or bypass existing query limits.
- Cancel requests with **Cancel request**. The client timeout is three minutes; cancellation propagates to the API, but already completed queries or model calls may still incur costs.
- Requests that fail leave the prompt available for retry. A reply with no successful tool calls shows the agent message without a table.

## Verification checklist

1. Build the solution; start both HTTPS profiles.
2. Ask for monthly sales for all dates. Verify the message and actual data table appear.
3. Request 30 products and verify the table's next/previous page controls.
4. Ask a query with no matching rows and verify its empty-result message.
5. Ask an unsupported request or omit a required ID and verify the agent message appears without invented rows.
6. Cancel a request, retry it, and verify controls are re-enabled.
7. Stop the API and submit a prompt; verify the connectivity error and retained prompt.
8. Check browser network responses for allowed CORS headers. A preflight from an unlisted origin must not receive Access-Control-Allow-Origin.
9. Confirm table cells and messages containing markup are displayed as text, not executed.
10. Start a fresh chat with “Show revenue by region”. Verify it asks for the region without running a query. Reply “Northwest”, then “all available dates” when asked for dates. Verify the resulting query uses Northwest and no date bounds.
11. Clear chat and submit “Show revenue for Northwest using all available dates”. Verify no redundant question is asked. Then clear again and confirm an isolated “Northwest” does not inherit the earlier request.
12. Submit “Show sales details for all available dates”. Verify the agent asks which sales report is wanted, remembers the date choice, and does not ask for it again.

Live responses require working SQL Server and Azure OpenAI configuration. Build success alone does not verify those services or browser behavior.
