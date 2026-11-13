using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Puts an emoji reaction on a chat message. Created per request.</summary>
public class ReactionChatTool : IChatTool
{
    private const int MaxReactionsPerRequest = 3;

    // Bots can only use the standard reaction set
    private static readonly string[] AllowedEmoji =
    [
        "❤", "👍", "👎", "🔥", "🥰", "👏", "😁", "🤔", "🤯", "😱", "🤬", "😢", "🎉", "🤩", "🤮", "💩", "🙏", "👌", "🕊",
        "🤡", "🥱", "🥴", "😍", "🐳", "❤‍🔥", "🌚", "🌭", "💯", "🤣", "⚡", "🍌", "🏆", "💔", "🤨", "😐", "🍓", "🍾", "💋",
        "🖕", "😈", "😴", "😭", "🤓", "👻", "👨‍💻", "👀", "🎃", "🙈", "😇", "😨", "🤝", "✍", "🤗", "🫡", "🎅", "🎄", "☃",
        "💅", "🤪", "🗿", "🆒", "💘", "🙉", "🦄", "😘", "💊", "🙊", "😎", "👾", "🤷‍♂", "🤷", "🤷‍♀", "😡"
    ];

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private int _reactionCount;

    public ReactionChatTool(ITelegramBotClient telegramBotClient, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "react_to_message";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "react_to_message",
        functionDescription: "Поставить реакцию-эмодзи на сообщение в чате, как живой участник. " +
                             "Используй, когда реакция уместна сама по себе: смешная шутка, похвала, абсурд, кринж, согласие. " +
                             "Не ставь реакцию на каждое сообщение — только когда она добавляет эмоцию. " +
                             "Если реакции достаточно и сказать нечего, можешь после неё ответить пустым текстом.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                emoji = new
                {
                    type = "string",
                    @enum = AllowedEmoji,
                    description = "Эмодзи реакции, только из списка"
                },
                target = new
                {
                    type = "string",
                    @enum = new[] { "request", "replied" },
                    description = "На какое сообщение: request — сообщение собеседника (по умолчанию), " +
                                  "replied — сообщение, на которое собеседник ответил"
                },
                message_id = new
                {
                    type = "integer",
                    description = "id конкретного сообщения из search_chat_history; если указан, target не нужен"
                }
            },
            required = new[] { "emoji" }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        if (_reactionCount >= MaxReactionsPerRequest)
        {
            return ToolArguments.Error("Хватит реакций на этот раз");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var emoji = ToolArguments.GetString(root, "emoji");
        var allowed = emoji == null
            ? null
            : AllowedEmoji.FirstOrDefault(e => ToolArguments.NormalizeEmoji(e) == ToolArguments.NormalizeEmoji(emoji));
        if (allowed == null)
        {
            return ToolArguments.Error("Такой реакции нет, выбери эмодзи из списка");
        }

        var request = _context.Request;
        long? messageId = ToolArguments.GetLong(root, "message_id");
        if (messageId == null && ToolArguments.GetString(root, "target") == "replied")
        {
            if (request.ReplyToMessage == null)
            {
                return ToolArguments.Error("Собеседник ни на что не отвечал");
            }
            messageId = request.ReplyToMessage.Id;
        }
        messageId ??= request.Id;

        try
        {
            await _telegramBotClient.SetMessageReaction(request.Chat.Id, (int)messageId, [allowed], cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to react {Emoji} to message {MessageId} in chat {ChatId}", allowed, messageId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось поставить реакцию: {ex.Message}");
        }

        _reactionCount++;
        _context.MarkActionTaken();
        _logger.LogInformation("Reacted {Emoji} to message {MessageId} in chat {ChatId}", allowed, messageId, request.Chat.Id);
        return ToolArguments.Result(new { ok = true, note = "Реакция поставлена" });
    }
}
