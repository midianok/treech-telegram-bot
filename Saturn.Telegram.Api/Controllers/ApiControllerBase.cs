using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Middleware;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected long? GetCurrentUserId()
    {
        return HttpContext.Items.TryGetValue(TelegramInitDataMiddleware.UserIdItemKey, out var v) && v is long id
            ? id
            : null;
    }

    protected TelegramInitDataUser? GetCurrentUser()
    {
        return HttpContext.Items.TryGetValue(TelegramInitDataMiddleware.UserItemKey, out var v)
            ? v as TelegramInitDataUser
            : null;
    }

    /// <summary>
    /// Пользователь из БД; если бот его ещё не знает (не писал в чат), создаётся из проверенного initData —
    /// иначе результат такого игрока в мини-играх отклонялся бы.
    /// </summary>
    protected async Task<UserEntity?> GetOrCreateUserAsync(SaturnContext db, long userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is not null)
        {
            return user;
        }

        var initDataUser = GetCurrentUser();
        if (initDataUser is null || initDataUser.Id != userId)
        {
            return null;
        }

        var entity = new UserEntity
        {
            Id = initDataUser.Id,
            Username = initDataUser.Username,
            FirstName = initDataUser.FirstName,
            LastName = initDataUser.LastName,
        };

        db.Users.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Параллельный запрос (или бот) успел создать пользователя.
            db.Entry(entity).State = EntityState.Detached;
            return await db.Users.FindAsync([userId], cancellationToken);
        }

        return entity;
    }
}
