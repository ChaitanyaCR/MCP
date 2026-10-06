using Microsoft.Extensions.AI;

namespace IncidentPolisher.Api;

/// <summary>Returns a fixed demo response without contacting an LLM.</summary>
public sealed class FakeChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string reply = """
            {
              "summary": "Demo: elevated API latency is being investigated.",
              "status": "Investigating (demo)",
              "update": "Demo response: The team is investigating elevated API latency and assessing service impact. The next update will follow once more information is available. This is sample data, not an analysis of your incident note.",
              "missingInformation": ["Demo: root cause has not been confirmed", "Demo: customer impact is being assessed"]
            }
            """;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
