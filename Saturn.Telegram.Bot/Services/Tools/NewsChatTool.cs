using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.NewsClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;

namespace Saturn.Bot.Service.Services.Tools;

public class NewsChatTool : IChatTool
{
    public string FunctionName => "get_news";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_news",
        functionDescription:
        "Получить свежие новости из RSS-ленты ТАСС: заголовок, краткое описание, время публикации, рубрики и ссылку. " +
        "В ленте около 100 последних новостей — это примерно последние несколько часов, более старых там нет. " +
        "Используй, когда спрашивают, что нового, что происходит в мире или в стране, просят пересказать новости " +
        "или обсуждают недавнее событие, о котором ты можешь не знать. " +
        "Для фактов из прошлого используй Википедию, а не новости.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description =
                        "Ключевые слова через пробел для отбора новостей, например «Украина переговоры», «ЦБ ставка» или «спорт». " +
                        "Ищутся в заголовке, описании и рубриках; подходят новости, где встречается хотя бы одно слово. " +
                        "Не указывай, чтобы получить все последние новости"
                },
                limit = new
                {
                    type = "integer",
                    description =
                        $"Сколько новостей вернуть, по умолчанию {NewsClient.DefaultLimit}, максимум {NewsClient.MaxLimit}"
                }
            }
        })
    );

    private readonly NewsClient _newsClient;

    public NewsChatTool(NewsClient newsClient) => _newsClient = newsClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var query = ToolArguments.GetString(doc.RootElement, "query");
        var limit = (int)(ToolArguments.GetLong(doc.RootElement, "limit") ?? NewsClient.DefaultLimit);

        return await _newsClient.GetNewsAsync(query, limit, сancellationToken);
    }
}