namespace Infrastructure;

public static class AITelemetry
{
    // UseOpenTelemetry uses this name for both the ActivitySource (traces) and the Meter (GenAI metrics)
    public const string SourceName = "ChatClient.AI";
}
