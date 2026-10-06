using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IncidentPolisher.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IncidentPolisher.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task FakeProviderWorksThroughEndpointWithoutAnLlm()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("AI:Provider", "Fake"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/incidents/polish",
            new { incidentNote = "A test incident.", audience = "Leadership" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PolishIncidentResponse>();
        Assert.StartsWith("Demo:", result!.Summary);
        Assert.Contains("sample data", result.Update);
        Assert.NotEmpty(result.MissingInformation);
    }

    private static WebApplicationFactory<Program> CreateApi(RecordingChatClient chatClient) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(chatClient);
            }));

    [Theory]
    [InlineData("Engineering")]
    [InlineData("Leadership")]
    [InlineData("Customer")]
    [InlineData(0)]
    public async Task EndpointReturnsStructuredUpdateForStringAndNumericAudiences(object audience)
    {
        var chat = new RecordingChatClient();
        using var factory = CreateApi(chat);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/incidents/polish", new { incidentNote = "Investigating latency.", audience });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PolishIncidentResponse>();
        Assert.Equal("Elevated API latency under investigation.", result!.Summary);
        Assert.Equal("Investigating", result.Status);
        Assert.Equal("Investigating elevated API latency.", result.Update);
        Assert.Equal(new[] { "Root cause has not been provided" }, result.MissingInformation);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "missingInformation", "status", "summary", "update" },
            json.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal("Investigating latency.", chat.Messages[1].Text);
    }

    [Theory]
    [InlineData("", "Engineering")]
    [InlineData(" ", "Customer")]
    [InlineData(null, "Leadership")]
    [InlineData("note", 99)]
    [InlineData("note", "Unknown")]
    public async Task InvalidInputReturnsBadRequestWithoutCallingModel(string? incidentNote, object audience)
    {
        var chat = new RecordingChatClient();
        using var factory = CreateApi(chat);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/incidents/polish", new { incidentNote, audience });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(chat.Messages);
    }

    [Theory]
    [InlineData("not valid JSON")]
    [InlineData("{}")]
    public async Task InvalidModelOutputReturnsBadGatewayWithoutExposingRawOutput(string reply)
    {
        using var factory = CreateApi(new RecordingChatClient { Reply = reply });
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/incidents/polish",
            new { incidentNote = "Investigating latency.", audience = "Engineering" });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid AI response", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("The AI provider did not return a valid structured incident update.",
            json.RootElement.GetProperty("detail").GetString());
    }
}
