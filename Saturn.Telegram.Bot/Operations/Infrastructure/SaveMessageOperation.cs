using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class SaveMessageOperation : IOperation
{
    private readonly ISaveMessageService _saveMessageService;

    public SaveMessageOperation(ISaveMessageService saveMessageService)
    {
        _saveMessageService = saveMessageService;
    }

    public bool Validate(Message msg, UpdateType type) => true;

    public Task OnMessageAsync(Message msg, UpdateType type) =>
        _saveMessageService.SaveMessageAsync(msg);

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
