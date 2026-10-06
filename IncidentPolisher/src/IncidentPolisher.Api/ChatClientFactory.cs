using System.ClientModel;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using OpenAI.Chat;

namespace IncidentPolisher.Api;

public static class ChatClientFactory
{
    public static IChatClient Create(IConfiguration configuration)
    {
        var provider = Required(configuration, "AI:Provider");
        return provider.ToLowerInvariant() switch
        {
            "fake" => new FakeChatClient(),
            "ollama" => new OllamaApiClient(
                Endpoint(configuration, "AI:Ollama:Endpoint"),
                Required(configuration, "AI:Ollama:Model")),
            "azurefoundry" => CreateAzureFoundryClient(configuration),
            _ => throw new InvalidOperationException(
                "AI:Provider must be Fake, Ollama, or AzureFoundry.")
        };
    }

    private static IChatClient CreateAzureFoundryClient(IConfiguration configuration)
    {
        var endpoint = Endpoint(configuration, "AI:AzureFoundry:Endpoint");
        if (endpoint.Scheme != Uri.UriSchemeHttps ||
            !endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AI:AzureFoundry:Endpoint must be an HTTPS OpenAI v1 endpoint ending in /openai/v1/.");
        }

        return new ChatClient(
            Required(configuration, "AI:AzureFoundry:Deployment"),
            new ApiKeyCredential(Required(configuration, "AI:AzureFoundry:ApiKey")),
            new OpenAIClientOptions { Endpoint = endpoint }).AsIChatClient();
    }

    private static Uri Endpoint(IConfiguration configuration, string key)
    {
        if (!Uri.TryCreate(Required(configuration, key), UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTP or HTTPS URL.");
        }

        return endpoint;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"Missing required configuration: {key}.");
    }
}
