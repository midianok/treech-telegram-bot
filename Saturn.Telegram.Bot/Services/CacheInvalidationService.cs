using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Db.CacheInvalidation;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services;

public class CacheInvalidationService(
    IConfiguration configuration,
    IChatCachedRepository chatCachedRepository,
    IImagePromptRepository imagePromptRepository,
    ILogger<CacheInvalidationService> logger) : BackgroundService
{
    private const string ListenCommand =
        $"LISTEN {CacheInvalidationChannels.Agent};" +
        $"LISTEN {CacheInvalidationChannels.Chat};" +
        $"LISTEN {CacheInvalidationChannels.ImagePrompt};";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetSectionOrThrow("CONNECTION_STRING");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);

                connection.Notification += (sender, args) => 
                    _ = HandleNotificationAsync(args, stoppingToken);

                await using (var command = new NpgsqlCommand(ListenCommand, connection))
                {
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CacheInvalidationService: connection lost, reconnecting in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleNotificationAsync(NpgsqlNotificationEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var notificationHandler = args.Channel switch
            {
                CacheInvalidationChannels.Agent => HandleAgentInvalidationAsync(args.Payload, cancellationToken),
                CacheInvalidationChannels.Chat => HandleChatInvalidationAsync(args.Payload, cancellationToken),
                CacheInvalidationChannels.ImagePrompt => HandleImagePromptInvalidationAsync(),
                _ => Task.CompletedTask
            };
            await notificationHandler;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CacheInvalidationService: error handling notification from channel {Channel}", args.Channel);
        }
    }

    private async Task HandleAgentInvalidationAsync(string payload, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(payload, out var agentId))
        {
            logger.LogWarning("Received invalid {Channel} payload: {Payload}", CacheInvalidationChannels.Agent, payload);
            return;
        }

        logger.LogInformation("Invalidating cache for agent {AgentId}", agentId);
        await chatCachedRepository.InvalidateByAgentAsync(agentId, cancellationToken);
    }

    private async Task HandleChatInvalidationAsync(string payload, CancellationToken cancellationToken)
    {
        if (!long.TryParse(payload, out var chatId))
        {
            logger.LogWarning("Received invalid {Channel} payload: {Payload}", CacheInvalidationChannels.Chat, payload);
            return;
        }

        logger.LogInformation("Invalidating cache for chat {ChatId}", chatId);
        await chatCachedRepository.InvalidateChatAsync(chatId);
    }

    private async Task HandleImagePromptInvalidationAsync()
    {
        logger.LogInformation("Invalidating image prompts cache");
        await imagePromptRepository.InvalidateAsync();
    }
}
