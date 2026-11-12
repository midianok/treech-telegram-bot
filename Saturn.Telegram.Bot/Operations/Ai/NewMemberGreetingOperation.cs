using System.Text;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class NewMemberGreetingOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<NewMemberGreetingOperation> _logger;

    public NewMemberGreetingOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IChatCachedRepository chatCachedRepository,
        ISaveMessageService saveMessageService,
        ILogger<NewMemberGreetingOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _chatCachedRepository = chatCachedRepository;
        _saveMessageService = saveMessageService;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        msg is { Type: MessageType.NewChatMembers, NewChatMembers: not null } &&
        msg.NewChatMembers.Any(x => !x.IsBot);

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing, cancellationToken: сancellationToken);

        var members = await Task.WhenAll(msg.NewChatMembers!
            .Where(x => !x.IsBot)
            .Select(async user => (User: user, AvatarDescription: await DescribeAvatarAsync(user, сancellationToken))));

        var greeting = await GenerateGreetingAsync(msg.Chat.Id, members, сancellationToken);

        var reply = await _telegramBotClient.SendMessage(msg.Chat, greeting,
            ParseMode.None, new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
        await _saveMessageService.SaveMessageAsync(reply);
    }

    private async Task<string?> DescribeAvatarAsync(User user, CancellationToken сancellationToken)
    {
        try
        {
            var photos = await _telegramBotClient.GetUserProfilePhotos(user.Id, limit: 1, cancellationToken: сancellationToken);
            var fileId = photos.Photos.FirstOrDefault()?.MaxBy(x => x.FileSize)?.FileId;
            if (string.IsNullOrEmpty(fileId))
            {
                return null;
            }

            var fileData = await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken);

            return await _aiService.CompleteChatAsync(
            [
                new UserChatMessage
                {
                    Content =
                    {
                        ChatMessageContentPart.CreateImagePart(new BinaryData(fileData), "image/jpg", ChatImageDetailLevel.Auto),
                        ChatMessageContentPart.CreateTextPart(
                            "Это аватарка пользователя Telegram. Внимательно изучи изображение.\n\n" +
                            "Сначала кратко опиши, что на нём изображено: кто или что, внешность, одежда, " +
                            "поза, выражение лица, фон, предметы и общая атмосфера. " +
                            "Отметь наиболее характерные и необычные детали.\n\n" +
                            "Затем выступи в роли язвительного и наблюдательного собеседника и оцени аватарку:\n" +
                            "- Что она может говорить о характере, вкусах, самооценке или амбициях владельца?\n" +
                            "- Какое первое впечатление производит человек с такой аватаркой?\n" +
                            "- Есть ли в ней что-то нелепое, комичное, чрезмерно пафосное, странное или клишированное?\n" +
                            "- Можно ли придумать забавную историю о том, почему человек выбрал именно такое изображение?\n" +
                            "- Найди одну-две детали, которые можно особенно остроумно обыграть.\n\n" +
                            "Стиль: саркастичный, слегка токсичный, дерзкий, с хорошим чувством юмора. " +
                            "Не будь чрезмерно вежливым и не пытайся обязательно похвалить аватарку. " +
                            "Ищи повод для шутки даже в обычных изображениях, но не вымучивай её, если материал действительно отсутствует.\n\n" +
                            "Важно:\n" +
                            "- Не ограничивайся перечислением объектов — интерпретируй увиденное.\n" +
                            "- Не выдавай предположения о личности владельца за факты.\n" +
                            "- Не придумывай детали, которых нет на изображении.\n" +
                            "- Не используй оскорбления внешности, инвалидности или других чувствительных характеристик.\n" +
                            "- Избегай банальных шуток и шаблонных комплиментов.\n" +
                            "- Не используй никакую разметку.\n" +
                            "Пиши по-русски, 3–4 предложения.")
                    }
                }
            ], сancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to describe avatar of user {UserId}", user.Id);
            return null;
        }
    }

    private async Task<string> GenerateGreetingAsync(long chatId, IReadOnlyList<(User User, string? AvatarDescription)> members, CancellationToken сancellationToken)
    {
        var membersInfo = new StringBuilder();
        foreach (var (user, avatarDescription) in members)
        {
            var name = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrEmpty(x)));
            var username = !string.IsNullOrEmpty(user.Username) ? $"@{user.Username}" : "нет";

            membersInfo.AppendLine($"Имя: {name}");
            membersInfo.AppendLine($"Юзернейм: {username}");
            membersInfo.AppendLine($"Аватарка: {avatarDescription ?? "нет или скрыта"}");
            membersInfo.AppendLine();
        }


        var prompt =
            (members.Count == 1
                ? "В групповой чат только что зашёл новый участник.\n"
                : "В групповой чат только что зашли новые участники.\n") +
            membersInfo +
            "Напиши провокационное приветственное сообщение для новичка, чтобы ему захотелось ответить и остаться в чате.\n\n" +
            "Тон: слегка токсичный, саркастичный, дерзкий, с хорошим чувством юмора. " +
            "Не будь стандартным дружелюбным ботом — веди себя как язвительный завсегдатай чата, " +
            "который с подозрением встречает каждого нового человека.\n\n" +
            "Обязательно используй следующие приёмы:\n" +
            "1. Колко и остроумно обыграй ник, имя и юзернейм новичка. " +
            "Попробуй предположить по ним, что это за персонаж и зачем он сюда пришёл.\n" +
            "2. Если доступна аватарка, используй её как дополнительный материал для подкола. " +
            "Если аватарки нет — высмей эту загадочность, анонимность или нежелание показать лицо.\n" +
            "3. Обязательно обыграй сценарий, при котором новичок сейчас же выйдет из чата. " +
            "Придумай забавное, абсурдное или слегка угрожающее последствие его побега. " +
            "Например: его объявят NPC, внесут в чёрный список чата, начнут розыск, " +
            "лишат права на интернет или запомнят как человека, который не выдержал даже приветствия. " +
            "Не используй эти примеры дословно — придумывай новые варианты.\n" +
            "4. Закончи провокационным вопросом или вызовом, на который новичку захочется ответить. " +
            "Не задавай банальное «Как дела?» или «Расскажи о себе».\n\n" +
            "Правила:\n" +
            "- Если есть юзернейм, обращайся через него, начиная с символа @.\n" +
            "- Не выдумывай факты о человеке, которых нет в предоставленной информации.\n" +
            "- Не переходи на реальные личные оскорбления, угрозы или унижение.\n" +
            "- Избегай шаблонных приветствий и избитых шуток.\n" +
            "- Каждый раз придумывай оригинальную шутку, не повторяй одни и те же приёмы.\n" +
            "- Не превращай сообщение в список характеристик новичка. Всё должно звучать как единая естественная реплика.\n" +
            "- Если участников несколько, постарайся обыграть их появление вместе.\n" +
            "- Не используй никакую разметку.\n" +
            "- Пиши по-русски, 3–5 предложений.";
        ;

        var messages = new List<ChatMessage>();
        var chatEntity = await _chatCachedRepository.GetAsync(chatId);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }
        messages.Add(new UserChatMessage(prompt));

        return await _aiService.CompleteChatAsync(messages, сancellationToken);
    }
}
