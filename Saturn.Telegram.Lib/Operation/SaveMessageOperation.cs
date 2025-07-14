using Saturn.Telegram.Lib.Services;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Operation;

public class SaveMessageOperation : IOperation
{
    private readonly ISaveMessageService _saveMessageService;

    public SaveMessageOperation(ISaveMessageService saveMessageService)
    {
        _saveMessageService = saveMessageService;
    }

    public Task OnMessageAsync(Message msg, UpdateType type) => 
        _saveMessageService.SaveMessageAsync(msg);

    public Task OnUpdateAsync(Update update) =>
        Task.CompletedTask;

    public Task OnErrorAsync(Exception exception, HandleErrorSource source) =>
        Task.CompletedTask;
}