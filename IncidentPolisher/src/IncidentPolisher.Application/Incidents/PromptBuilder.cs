namespace IncidentPolisher.Application.Incidents;

public sealed class PromptBuilder(string promptDirectory)
{
    public async Task<string> BuildAsync(
        IncidentAudience audience,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(audience))
        {
            throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown incident audience.");
        }

        var systemRules = await ReadPromptAsync(
            Path.Combine(promptDirectory, "System.md"), cancellationToken);
        var audienceRules = await ReadPromptAsync(
            Path.Combine(promptDirectory, $"{audience}.md"), cancellationToken);
        var responseFormat = await ReadPromptAsync(
            Path.Combine(promptDirectory, "ResponseFormat.md"), cancellationToken);

        return $"{systemRules}\n\n{audienceRules}\n\n{responseFormat}";
    }

    private static async Task<string> ReadPromptAsync(string path, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Prompt file '{path}' must not be empty.");
        }

        return content.Trim();
    }
}
