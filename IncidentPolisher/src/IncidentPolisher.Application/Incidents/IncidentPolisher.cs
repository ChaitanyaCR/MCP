using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace IncidentPolisher.Application.Incidents;

public sealed class IncidentPolisher(
    IChatClient chatClient,
    PromptBuilder promptBuilder) : IIncidentPolisher
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<PolishedIncident> PolishAsync(
        string incident,
        IncidentAudience audience,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incident);

        var systemPrompt = await promptBuilder.BuildAsync(audience, cancellationToken);

        var messages = new ChatMessage[]
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, incident)
        };

        var response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        PolishedIncident? result;
        try
        {
            result = JsonSerializer.Deserialize<PolishedIncident>(response.Text, ResponseJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidIncidentResponseException(exception);
        }

        if (result is null ||
            string.IsNullOrWhiteSpace(result.Summary) ||
            string.IsNullOrWhiteSpace(result.Status) ||
            string.IsNullOrWhiteSpace(result.Update) ||
            result.MissingInformation is null ||
            result.MissingInformation.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidIncidentResponseException();
        }

        return result with
        {
            Summary = result.Summary.Trim(),
            Status = result.Status.Trim(),
            Update = result.Update.Trim(),
            MissingInformation = result.MissingInformation.Select(value => value.Trim()).ToArray()
        };
    }
}
