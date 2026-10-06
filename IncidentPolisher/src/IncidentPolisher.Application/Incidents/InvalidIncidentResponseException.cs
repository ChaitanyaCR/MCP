namespace IncidentPolisher.Application.Incidents;

public sealed class InvalidIncidentResponseException : Exception
{
    public InvalidIncidentResponseException()
        : base("The AI provider returned an invalid incident response.") { }

    public InvalidIncidentResponseException(Exception innerException)
        : base("The AI provider returned an invalid incident response.", innerException) { }
}
