namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IOperationCallRepository
{
    Task RecordAsync(string operationName, long chatId, long userId);
}
