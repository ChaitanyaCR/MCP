using IncidentPolisher.Application.Incidents;
using Microsoft.Extensions.AI;
using Polisher = IncidentPolisher.Application.Incidents.IncidentPolisher;

namespace IncidentPolisher.Tests;

public sealed class PolishingTests : IDisposable
{
    private readonly string promptDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PolishingTests()
    {
        Directory.CreateDirectory(promptDirectory);
        foreach (var source in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Prompts"), "*.md"))
        {
            File.Copy(source, Path.Combine(promptDirectory, Path.GetFileName(source)));
        }
    }

    [Theory]
    [InlineData(IncidentAudience.Leadership, "business/service impact")]
    [InlineData(IncidentAudience.Engineering, "components")]
    [InlineData(IncidentAudience.Customer, "non-technical language")]
    public async Task PolisherCombinesSharedAndSelectedRulesAndForwardsCancellation(
        IncidentAudience audience, string guidance)
    {
        var chatClient = new RecordingChatClient();
        var polisher = new Polisher(chatClient, new PromptBuilder(promptDirectory));
        using var cancellation = new CancellationTokenSource();
        const string incident = "Cause unclear; investigating elevated API latency.";

        var result = await polisher.PolishAsync(incident, audience, cancellation.Token);

        Assert.Equal("Elevated API latency under investigation.", result.Summary);
        Assert.Equal("Investigating", result.Status);
        Assert.Equal("Investigating elevated API latency.", result.Update);
        Assert.Equal(new[] { "Root cause has not been provided" }, result.MissingInformation);
        Assert.Same(ChatResponseFormat.Json, chatClient.Options!.ResponseFormat);
        Assert.Equal(cancellation.Token, chatClient.CancellationToken);
        Assert.Equal(2, chatClient.Messages.Count);
        Assert.Equal(ChatRole.System, chatClient.Messages[0].Role);
        Assert.Contains("Do not invent information.", chatClient.Messages[0].Text);
        Assert.Contains("preserve that uncertainty", chatClient.Messages[0].Text);
        Assert.Contains("explicitly indicates that it is resolved", chatClient.Messages[0].Text);
        Assert.Contains($"Audience: {audience}", chatClient.Messages[0].Text);
        Assert.Contains(guidance, chatClient.Messages[0].Text);
        Assert.Contains("\"missingInformation\"", chatClient.Messages[0].Text);
        Assert.Contains("Use \"Unknown\"", chatClient.Messages[0].Text);
        foreach (var other in Enum.GetValues<IncidentAudience>().Where(value => value != audience))
            Assert.DoesNotContain($"Audience: {other}", chatClient.Messages[0].Text);
        Assert.Equal(ChatRole.User, chatClient.Messages[1].Role);
        Assert.Equal(incident, chatClient.Messages[1].Text);
    }

    [Fact]
    public async Task PromptEditsApplyToNextRequestWithoutRecreatingBuilder()
    {
        var builder = new PromptBuilder(promptDirectory);
        await builder.BuildAsync(IncidentAudience.Leadership, default);
        await File.WriteAllTextAsync(Path.Combine(promptDirectory, "System.md"), "Updated shared rules");
        await File.WriteAllTextAsync(Path.Combine(promptDirectory, "Leadership.md"), "Updated audience guidance");
        await File.WriteAllTextAsync(Path.Combine(promptDirectory, "ResponseFormat.md"), "Updated output guidance");
        Assert.Equal("Updated shared rules\n\nUpdated audience guidance\n\nUpdated output guidance",
            await builder.BuildAsync(IncidentAudience.Leadership, default));
    }

    [Fact]
    public async Task MissingAndEmptyPromptFilesFailBeforeModelCall()
    {
        var client = new RecordingChatClient();
        var polisher = new Polisher(client, new PromptBuilder(promptDirectory));
        var path = Path.Combine(promptDirectory, "System.md");
        await File.WriteAllTextAsync(path, " ");
        await Assert.ThrowsAsync<InvalidOperationException>(() => polisher.PolishAsync("note", IncidentAudience.Customer, default));
        File.Delete(path);
        await Assert.ThrowsAsync<FileNotFoundException>(() => polisher.PolishAsync("note", IncidentAudience.Customer, default));
        Assert.Empty(client.Messages);
    }

    [Fact]
    public async Task CancelledRequestsAndUnknownAudiencesNeverCallModel()
    {
        var client = new RecordingChatClient();
        var polisher = new Polisher(client, new PromptBuilder(promptDirectory));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => polisher.PolishAsync("note", IncidentAudience.Engineering, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => polisher.PolishAsync("note", (IncidentAudience)99, default));
        Assert.Empty(client.Messages);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Plain text update")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\"}")]
    [InlineData("{\"summary\":null,\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":[]}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\" \",\"update\":\"Update\",\"missingInformation\":[]}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\" \",\"missingInformation\":[]}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":null}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":\"Unknown\"}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":[null]}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":[\" \"]}")]
    [InlineData("{\"summary\":\"Summary\",\"status\":\"Monitoring\",\"update\":\"Update\",\"missingInformation\":[],\"extra\":true}")]
    public async Task InvalidStructuredModelResponsesFail(string reply)
    {
        var polisher = new Polisher(new RecordingChatClient { Reply = reply }, new PromptBuilder(promptDirectory));
        await Assert.ThrowsAsync<InvalidIncidentResponseException>(() => polisher.PolishAsync("note", IncidentAudience.Customer, default));
    }

    [Fact]
    public async Task ValidResponseCanHaveNoMissingInformation()
    {
        var client = new RecordingChatClient
        {
            Reply = """{"summary":"Summary","status":"Resolved","update":"Update","missingInformation":[]}"""
        };
        var polisher = new Polisher(client, new PromptBuilder(promptDirectory));
        var result = await polisher.PolishAsync("Incident explicitly resolved.", IncidentAudience.Customer, default);
        Assert.Empty(result.MissingInformation);
    }

    [Fact]
    public async Task MissingOutputInstructionsFailBeforeModelCall()
    {
        File.Delete(Path.Combine(promptDirectory, "ResponseFormat.md"));
        var client = new RecordingChatClient();
        var polisher = new Polisher(client, new PromptBuilder(promptDirectory));
        await Assert.ThrowsAsync<FileNotFoundException>(() => polisher.PolishAsync("note", IncidentAudience.Customer, default));
        Assert.Empty(client.Messages);
    }

    public void Dispose() => Directory.Delete(promptDirectory, recursive: true);
}
