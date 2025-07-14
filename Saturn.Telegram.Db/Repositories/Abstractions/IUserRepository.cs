using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IUserRepository
{
    Task<UserEntity> GetCachedAsync(long userId);
}