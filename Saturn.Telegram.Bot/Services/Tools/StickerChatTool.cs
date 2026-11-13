using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Sends one of the chat's own popular stickers matching a mood. Created per request.</summary>
public class StickerChatTool : IChatTool
{
    private const int PopularStickerCount = 50;
    private const int RandomPickAmong = 3;

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _stickerSent;

    public StickerChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "send_sticker";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "send_sticker",
        functionDescription: "Отправить в чат стикер из тех, что участники чата сами часто шлют, подобранный по эмодзи-настроению. " +
                             "Используй изредка, когда стикер выразит эмоцию лучше слов: ржач, фейспалм, злость, одобрение. " +
                             "Если стикера достаточно и сказать нечего, можешь после него ответить пустым текстом.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                emoji = new
                {
                    type = "string",
                    description = "Одно эмодзи, передающее настроение стикера, например 😂, 😡, 👍, 🤔, 😭. " +
                                  "Если стикера с таким эмодзи нет, вернётся список доступных — выбери из него"
                }
            },
            required = new[] { "emoji" }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        if (_stickerSent)
        {
            return ToolArguments.Error("Один стикер за ответ, хватит");
        }

        using var doc = JsonDocument.Parse(arguments);
        var emoji = ToolArguments.GetString(doc.RootElement, "emoji");
        if (emoji == null)
        {
            return ToolArguments.Error("Укажи эмодзи");
        }

        var chatId = _context.Request.Chat.Id;
        var stickers = await _messageRepository.GetPopularStickersAsync(chatId, PopularStickerCount, сancellationToken);
        if (stickers.Count == 0)
        {
            return ToolArguments.Error("В этом чате ещё не слали стикеров");
        }

        var labelled = stickers.Where(s => s.Emoji != null).ToList();
        var normalizedEmoji = ToolArguments.NormalizeEmoji(emoji);
        var matches = labelled.Where(s => ToolArguments.NormalizeEmoji(s.Emoji!) == normalizedEmoji).ToList();

        PopularSticker sticker;
        if (matches.Count > 0)
        {
            sticker = matches.Take(RandomPickAmong).ElementAt(Random.Shared.Next(Math.Min(RandomPickAmong, matches.Count)));
        }
        else if (labelled.Count > 0)
        {
            return ToolArguments.Result(new
            {
                error = $"Стикера с {emoji} нет",
                available_emoji = labelled.Select(s => s.Emoji).Distinct()
            });
        }
        else
        {
            // Emoji are only known for stickers saved recently; until then the chat's favourites are a safe bet
            sticker = stickers.Take(RandomPickAmong).ElementAt(Random.Shared.Next(Math.Min(RandomPickAmong, stickers.Count)));
        }

        Message sent;
        try
        {
            sent = await _telegramBotClient.SendSticker(chatId, new InputFileId(sticker.StickerId),
                replyParameters: new ReplyParameters { MessageId = _context.Request.Id, AllowSendingWithoutReply = true },
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to send sticker {StickerId} to chat {ChatId}", sticker.StickerId, chatId);
            return ToolArguments.Error("Не удалось отправить стикер");
        }

        // Learn the emoji of old stickers saved before it was stored
        if (sticker.Emoji == null && !string.IsNullOrEmpty(sent.Sticker?.Emoji))
        {
            await _messageRepository.SetStickerEmojiAsync(sticker.StickerId, sent.Sticker.Emoji, сancellationToken);
        }

        _stickerSent = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Sent sticker {StickerId} ({Emoji}, used {Count} times) for requested {RequestedEmoji} to chat {ChatId}",
            sticker.StickerId, sent.Sticker?.Emoji, sticker.UseCount, emoji, chatId);
        return ToolArguments.Result(new { ok = true, sticker_emoji = sent.Sticker?.Emoji, note = "Стикер отправлен" });
    }
}
