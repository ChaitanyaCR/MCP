using Microsoft.Extensions.AI;

namespace IncidentPolisher.Tests;

internal sealed class RecordingChatClient : IChatClient
{
    public string Reply { get; set; } = """
        {
          "summary": "Elevated API latency under investigation.",
          "status": "Investigating",
          "update": "  Investigating elevated API latency.  ",
          "missingInformation": ["Root cause has not been provided"]
        }
        """;
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
    public CancellationToken CancellationToken { get; private set; }
    public ChatOptions? Options { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Messages = messages.ToArray();
        CancellationToken = cancellationToken;
        Options = options;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
