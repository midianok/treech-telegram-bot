using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Saturn.Bot.Service.Services;

public class YtDlpUpdateService : BackgroundService
{
    private readonly ILogger<YtDlpUpdateService> _logger;

    public YtDlpUpdateService(ILogger<YtDlpUpdateService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await YtDlpSetupService.RunSelfUpdateAsync(_logger, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update yt-dlp");
            }
        }
    }
}
