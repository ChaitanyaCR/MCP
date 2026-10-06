using System.Text.Json.Serialization;
using IncidentPolisher.Api;
using IncidentPolisher.Api.Contracts;
using IncidentPolisher.Application.Incidents;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<IncidentAudience>()));
builder.Services.AddSingleton<IChatClient>(_ => ChatClientFactory.Create(builder.Configuration));
builder.Services.AddSingleton(provider => new PromptBuilder(
    Path.Combine(provider.GetRequiredService<IHostEnvironment>().ContentRootPath, "Prompts")));
builder.Services.AddScoped<IIncidentPolisher, IncidentPolisher.Application.Incidents.IncidentPolisher>();
var app = builder.Build();
// Validate the selected provider's settings at startup, without making a model call.
_ = app.Services.GetRequiredService<IChatClient>();

app.MapPost("/api/incidents/polish", async (
    PolishIncidentRequest request,
    IIncidentPolisher polisher,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.IncidentNote))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.IncidentNote)] = ["IncidentNote is required."]
        });
    }

    if (!Enum.IsDefined(request.Audience))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Audience)] = ["Audience must be Engineering, Leadership, or Customer."]
        });
    }

    try
    {
        var result = await polisher.PolishAsync(
            request.IncidentNote, request.Audience, cancellationToken);
        return Results.Ok(new PolishIncidentResponse(
            result.Summary, result.Status, result.Update, result.MissingInformation));
    }
    catch (InvalidIncidentResponseException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Invalid AI response",
            detail: "The AI provider did not return a valid structured incident update.");
    }
});

app.Run();

public partial class Program { }
