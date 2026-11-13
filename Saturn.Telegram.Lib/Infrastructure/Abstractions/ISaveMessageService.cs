using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib.Infrastructure.Abstractions;

public interface ISaveMessageService
{
    /// <param name="msg">Message to save.</param>
    /// <param name="aiToolCalls">For AI chat answers: names of the tools called while generating the answer (empty if none).</param>
    Task SaveMessageAsync(Message msg, IReadOnlyCollection<string>? aiToolCalls = null);
}
