using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>
/// Creates the AI chat tools bound to one request: they need to know which chat and message
/// they act on, so they can't be singletons like the stateless tools.
/// </summary>
public class ChatToolFactory
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly IReminderRepository _reminderRepository;
    private readonly IAiService _aiService;
    private readonly IGenerationProgressService _generationProgressService;
    private readonly ChatImageQuota _imageQuota;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string? _adminUsername;

    public ChatToolFactory(
        ITelegramBotClient telegramBotClient,
        IMessageRepository messageRepository,
        IReminderRepository reminderRepository,
        IAiService aiService,
        IGenerationProgressService generationProgressService,
        ChatImageQuota imageQuota,
        IOptions<BotOptions> botOptions,
        ILoggerFactory loggerFactory)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _reminderRepository = reminderRepository;
        _aiService = aiService;
        _generationProgressService = generationProgressService;
        _imageQuota = imageQuota;
        _loggerFactory = loggerFactory;
        _adminUsername = botOptions.Value.AdminUsername;
    }

    public IReadOnlyList<IChatTool> Create(ChatToolContext context)
    {
        var request = context.Request;
        var isAdmin = !string.IsNullOrEmpty(_adminUsername) &&
                      string.Equals(request.From?.Username, _adminUsername, StringComparison.OrdinalIgnoreCase);

        return
        [
            new ChatHistoryChatTool(_messageRepository, request.Chat.Id, _loggerFactory.CreateLogger<ChatHistoryChatTool>()),
            new UserProfileChatTool(_telegramBotClient, _aiService, _messageRepository, request.Chat.Id,
                _loggerFactory.CreateLogger<UserProfileChatTool>()),
            new ReactionChatTool(_telegramBotClient, context, _loggerFactory.CreateLogger<ReactionChatTool>()),
            new StickerChatTool(_telegramBotClient, _messageRepository, context, _loggerFactory.CreateLogger<StickerChatTool>()),
            new ImageChatTool(_telegramBotClient, _aiService, _generationProgressService, _imageQuota, context, isAdmin,
                _loggerFactory.CreateLogger<ImageChatTool>()),
            new PollChatTool(_telegramBotClient, context, _loggerFactory.CreateLogger<PollChatTool>()),
            new ReminderChatTool(_messageRepository, _reminderRepository, context, _loggerFactory.CreateLogger<ReminderChatTool>()),
            new ListRemindersChatTool(_reminderRepository, context),
            new CancelReminderChatTool(_reminderRepository, context, isAdmin, _loggerFactory.CreateLogger<CancelReminderChatTool>())
        ];
    }
}
