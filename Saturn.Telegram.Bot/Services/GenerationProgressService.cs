using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services;

public class GenerationProgressService : IGenerationProgressService
{
    private const string TimeoutText = "слишком долго генерировал, сдался";
    private const int MaxEstimatedPercent = 95;
    private const int BarLength = 10;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(4);

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly ILogger<GenerationProgressService> _logger;

    public GenerationProgressService(ITelegramBotClient telegramBotClient, ILogger<GenerationProgressService> logger)
    {
        _telegramBotClient = telegramBotClient;
        _logger = logger;
    }

    public async Task RunAsync<T>(
        Message replyTo,
        GenerationProgressOptions options,
        Func<Action<string>, Task<T>> generate,
        Func<T, Task> deliver,
        CancellationToken cancellationToken)
    {
        var initialText = BuildText(options, null, TimeSpan.Zero);
        var statusMessage = await _telegramBotClient.SendMessage(
            replyTo.Chat.Id,
            initialText,
            replyParameters: new ReplyParameters { MessageId = replyTo.MessageId },
            cancellationToken: cancellationToken);

        try
        {
            string? status = null;
            var stopwatch = Stopwatch.StartNew();
            var generateTask = generate(s => Volatile.Write(ref status, s));

            var lastText = initialText;
            while (!generateTask.IsCompleted)
            {
                var progressText = BuildText(options, Volatile.Read(ref status), stopwatch.Elapsed);
                if (progressText != lastText)
                {
                    await TryEditAsync(statusMessage, progressText);
                    lastText = progressText;
                }

                await TrySendChatActionAsync(replyTo, options, cancellationToken);
                await Task.WhenAny(generateTask, Task.Delay(UpdateInterval, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }

            var result = await generateTask;
            await deliver(result);
            await TryDeleteAsync(statusMessage);
        }
        catch (Exception ex) when (ex is AiContentModerationException or AiBudgetExhaustedException or AiEmptyResponseException)
        {
            // Ответ пользователю на эти ошибки отправляет OperationManager.
            await TryDeleteAsync(statusMessage);
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            await TryEditAsync(statusMessage, TimeoutText);
            throw;
        }
        catch
        {
            await TryEditAsync(statusMessage, options.FailureText);
            throw;
        }
    }

    private static string BuildText(GenerationProgressOptions options, string? status, TimeSpan elapsed)
    {
        var stage = status switch
        {
            null => "отправляю запрос",
            "created" or "queued" or "starting" => "в очереди",
            "processing" => options.ProcessingStage,
            "completed" or "succeeded" => "почти готово",
            _ => status
        };

        // AtlasCloud не отдаёт процент готовности — оцениваем по типичной длительности генерации.
        var percent = (int)Math.Min(MaxEstimatedPercent, elapsed / options.ExpectedDuration * 100);
        var filled = percent * BarLength / 100;
        var bar = new string('▓', filled) + new string('░', BarLength - filled);

        return $"{options.Title}\n" +
               $"Статус: {stage}\n" +
               $"{bar} ~{percent}%\n" +
               $"⏱ {elapsed:m\\:ss}";
    }

    private async Task TrySendChatActionAsync(Message replyTo, GenerationProgressOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await _telegramBotClient.SendChatAction(replyTo.Chat.Id, options.ChatAction, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to send chat action");
        }
    }

    private async Task TryEditAsync(Message message, string text)
    {
        try
        {
            await _telegramBotClient.EditMessageText(message.Chat.Id, message.MessageId, text);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to update generation status message");
        }
    }

    private async Task TryDeleteAsync(Message message)
    {
        try
        {
            await _telegramBotClient.DeleteMessage(message.Chat.Id, message.MessageId);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to delete generation status message");
        }
    }
}
