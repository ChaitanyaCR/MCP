var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("IncidentApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["IncidentApi:BaseUrl"] ?? "http://localhost:5080"));
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/incidents/polish", async (HttpRequest request, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    using var content = new StreamContent(request.Body);
    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    try
    {
        using var response = await clients.CreateClient("IncidentApi").PostAsync("/api/incidents/polish", content, cancellationToken);
        return Results.Content(await response.Content.ReadAsStringAsync(cancellationToken),
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }
    catch (HttpRequestException)
    {
        return Results.Problem(statusCode: 502, title: "API unavailable",
            detail: "Could not reach the incident API. Start it on the configured address and try again.");
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(statusCode: 504, title: "Request timed out",
            detail: "The incident API took too long to respond. Try again.");
    }
});

app.Run();
