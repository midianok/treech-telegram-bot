using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class SaveMessageOperation : OperationBase
{
    private readonly ISaveMessageService _saveMessageService;

    public SaveMessageOperation(ISaveMessageService saveMessageService)
    {
        _saveMessageService = saveMessageService;
    }
    protected override Task ProcessOnMessageAsync(Message msg, UpdateType type) => 
        _saveMessageService.SaveMessageAsync(msg);
}