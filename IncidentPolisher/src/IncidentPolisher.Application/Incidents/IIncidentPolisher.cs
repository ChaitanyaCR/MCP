namespace IncidentPolisher.Application.Incidents;

public interface IIncidentPolisher
{
    Task<PolishedIncident> PolishAsync(
        string incident,
        IncidentAudience audience,
        CancellationToken cancellationToken);
}
