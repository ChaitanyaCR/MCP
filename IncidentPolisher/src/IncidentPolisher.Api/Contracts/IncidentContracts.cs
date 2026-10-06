using IncidentPolisher.Application.Incidents;

namespace IncidentPolisher.Api.Contracts;

public record PolishIncidentRequest(
    string IncidentNote,
    IncidentAudience Audience);

public record PolishIncidentResponse(
    string Summary,
    string Status,
    string Update,
    string[] MissingInformation);
