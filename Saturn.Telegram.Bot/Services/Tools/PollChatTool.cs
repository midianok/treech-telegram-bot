using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Starts a poll in the chat. Created per request.</summary>
public class PollChatTool : IChatTool
{
    private const int MaxQuestionLength = 300;
    private const int MaxOptionLength = 100;
    private const int MinOptions = 2;
    private const int MaxOptions = 10;

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _pollSent;

    public PollChatTool(ITelegramBotClient telegramBotClient, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "create_poll";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "create_poll",
        functionDescription: "Запустить опрос в чате. Используй, когда просят устроить голосование " +
                             "или когда спор удобно решить голосованием («кто прав?», «куда идём в пятницу?»). " +
                             "Опрос уходит в чат сразу, текстом можешь коротко его представить.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                question = new
                {
                    type = "string",
                    description = $"Вопрос, до {MaxQuestionLength} символов"
                },
                options = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = $"Варианты ответа, от {MinOptions} до {MaxOptions}, каждый до {MaxOptionLength} символов"
                },
                is_anonymous = new
                {
                    type = "boolean",
                    description = "Анонимный ли опрос, по умолчанию нет — чтобы было видно, кто как голосовал"
                },
                allows_multiple_answers = new
                {
                    type = "boolean",
                    description = "Можно ли выбрать несколько вариантов, по умолчанию нет"
                }
            },
            required = new[] { "question", "options" }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        if (_pollSent)
        {
            return ToolArguments.Error("Один опрос за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var question = ToolArguments.GetString(root, "question");
        if (question == null || question.Length > MaxQuestionLength)
        {
            return ToolArguments.Error($"Нужен вопрос до {MaxQuestionLength} символов");
        }

        var options = ToolArguments.GetStrings(root, "options").Distinct().ToList();
        if (options.Count is < MinOptions or > MaxOptions || options.Any(o => o.Length > MaxOptionLength))
        {
            return ToolArguments.Error($"Нужно от {MinOptions} до {MaxOptions} разных вариантов, каждый до {MaxOptionLength} символов");
        }

        var request = _context.Request;
        try
        {
            await _telegramBotClient.SendPoll(
                request.Chat.Id,
                question,
                options.Select(o => new InputPollOption(o)).ToArray(),
                isAnonymous: ToolArguments.GetBool(root, "is_anonymous") ?? false,
                allowsMultipleAnswers: ToolArguments.GetBool(root, "allows_multiple_answers") ?? false,
                replyParameters: new ReplyParameters { MessageId = request.Id, AllowSendingWithoutReply = true },
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to create poll in chat {ChatId}", request.Chat.Id);
            return ToolArguments.Error($"Не удалось создать опрос: {ex.Message}");
        }

        _pollSent = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Created poll \"{Question}\" with {Count} options in chat {ChatId}", question, options.Count, request.Chat.Id);
        return ToolArguments.Result(new { ok = true, note = "Опрос отправлен в чат" });
    }
}
