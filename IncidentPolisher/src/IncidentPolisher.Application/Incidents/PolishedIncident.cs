using System.Text.Json.Serialization;

namespace IncidentPolisher.Application.Incidents;

public sealed record PolishedIncident(
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string Update,
    [property: JsonRequired] string[] MissingInformation);
