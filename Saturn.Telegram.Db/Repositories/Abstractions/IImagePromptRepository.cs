namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IImagePromptRepository
{
    Task<string?> FindPromptAsync(string query);
    Task InvalidateAsync();
}
