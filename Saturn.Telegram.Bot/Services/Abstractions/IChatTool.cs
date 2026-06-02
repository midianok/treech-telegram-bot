using OpenAI.Chat;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface IChatTool
{
    string FunctionName { get; }
    ChatTool Definition { get; }
    Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken);
}
