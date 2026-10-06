using IncidentPolisher.Api;
using Microsoft.Extensions.Configuration;

namespace IncidentPolisher.Tests;

public sealed class ProviderTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> FoundrySettings() => new()
    {
        ["AI:Provider"] = "AzureFoundry",
        ["AI:AzureFoundry:Endpoint"] = "https://example.openai.azure.com/openai/v1/",
        ["AI:AzureFoundry:Deployment"] = "test-deployment",
        ["AI:AzureFoundry:ApiKey"] = "dummy-test-key"
    };

    [Fact]
    public void ConfigurationSelectsBothAdaptersWithoutModelCalls()
    {
        using var ollama = ChatClientFactory.Create(Configuration(new()
        {
            ["AI:Provider"] = "Ollama",
            ["AI:Ollama:Endpoint"] = "http://localhost:11434",
            ["AI:Ollama:Model"] = "llama3.2"
        }));
        Assert.Contains("Ollama", ollama.GetType().Name);
        using var foundry = ChatClientFactory.Create(Configuration(FoundrySettings()));
        Assert.Contains("OpenAI", foundry.GetType().Name);
    }

    [Theory]
    [InlineData("AI:AzureFoundry:ApiKey", "", "AI:AzureFoundry:ApiKey")]
    [InlineData("AI:AzureFoundry:Deployment", "", "AI:AzureFoundry:Deployment")]
    [InlineData("AI:AzureFoundry:Endpoint", "https://example.services.ai.azure.com/api/projects/test", "OpenAI v1 endpoint")]
    [InlineData("AI:Provider", "Unknown", "AI:Provider")]
    public void InvalidProviderConfigurationFails(string key, string value, string expectedError)
    {
        var settings = FoundrySettings();
        settings[key] = value;
        var error = Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(Configuration(settings)));
        Assert.Contains(expectedError, error.Message);
    }
}
