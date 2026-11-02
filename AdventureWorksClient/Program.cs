using AdventureWorksClient;
using AdventureWorksClient.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttps && apiUri.Scheme != Uri.UriSchemeHttp))
{
    throw new InvalidOperationException("ApiBaseUrl must be an absolute HTTP or HTTPS address.");
}

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiUri.AbsoluteUri.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(3)
});
builder.Services.AddScoped<AgentApiClient>();

await builder.Build().RunAsync();
