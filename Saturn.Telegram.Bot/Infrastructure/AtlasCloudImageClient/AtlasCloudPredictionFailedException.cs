namespace Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;

public class AtlasCloudPredictionFailedException : InvalidOperationException
{
    public AtlasCloudPredictionFailedException(string status, string? error, string message)
        : base(message)
    {
        Status = status;
        Error = error;
    }

    public string Status { get; }

    public string? Error { get; }
}
