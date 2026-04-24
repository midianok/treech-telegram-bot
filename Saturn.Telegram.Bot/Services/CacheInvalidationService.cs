using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services;

public class CacheInvalidationService(
    IConfiguration configuration,
    IChatCachedRepository chatCachedRepository,
    IImagePromptRepository imagePromptRepository,
    ILogger<CacheInvalidationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetSectionOrThrow("CONNECTION_STRING");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(stoppingToken);

        conn.Notification += async (_, args) =>
        {
            switch (args.Channel)
            {
                case "agent_invalidation":
                {
                    if (Guid.TryParse(args.Payload, out var agentId))
                    {
                        logger.LogInformation("Invalidating cache for agent {AgentId}", agentId);
                        await chatCachedRepository.InvalidateByAgentAsync(agentId, stoppingToken);
                    }
                    else
                    {
                        logger.LogWarning("Received invalid agent_invalidation payload: {Payload}", args.Payload);
                    }

                    break;
                }
                case "chat_invalidation":
                {
                    if (long.TryParse(args.Payload, out var chatId))
                    {
                        logger.LogInformation("Invalidating cache for chat {ChatId}", chatId);
                        await chatCachedRepository.InvalidateChatAsync(chatId);
                    }
                    else
                    {
                        logger.LogWarning("Received invalid chat_invalidation payload: {Payload}", args.Payload);
                    }

                    break;
                }
                case "image_prompt_invalidation":
                {
                    logger.LogInformation("Invalidating image prompts cache");
                    await imagePromptRepository.InvalidateAsync();
                    break;
                }
            }
        };

        await using var agentCmd = new NpgsqlCommand("LISTEN agent_invalidation", conn);
        await agentCmd.ExecuteNonQueryAsync(stoppingToken);

        await using var chatCmd = new NpgsqlCommand("LISTEN chat_invalidation", conn);
        await chatCmd.ExecuteNonQueryAsync(stoppingToken);

        await using var imagePromptCmd = new NpgsqlCommand("LISTEN image_prompt_invalidation", conn);
        await imagePromptCmd.ExecuteNonQueryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await conn.WaitAsync(stoppingToken);
        }
    }
}
