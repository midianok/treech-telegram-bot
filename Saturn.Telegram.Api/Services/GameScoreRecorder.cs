using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Api.Services;

/// <summary>
/// Итог сохранения результата: новый личный рекорд, обогнанные игроки и обновлённый топ чата.
/// </summary>
public record GameRecordResult<TEntity>(
    IReadOnlyList<TEntity> Overtaken,
    IReadOnlyList<TEntity> Top) where TEntity : class, IGameScoreEntity;

public static class GameScoreRecorder
{
    private const int TopSize = 10;

    /// <summary>
    /// Сохраняет результат, если он лучше личного рекорда. Возвращает null, если рекорд не побит
    /// (объявлять в чате нечего). Первый ненулевой результат тоже считается рекордом.
    /// </summary>
    public static async Task<GameRecordResult<TEntity>?> SaveAsync<TEntity>(
        DbContext db,
        DbSet<TEntity> scores,
        long userId,
        long chatId,
        int score,
        Func<TEntity> create,
        CancellationToken cancellationToken) where TEntity : class, IGameScoreEntity
    {
        var existing = await scores.FindAsync([userId, chatId], cancellationToken);
        if (existing is not null && score <= existing.Score)
        {
            return null;
        }

        var previousScore = existing?.Score;

        // Игроки, которые были на уровне или выше прежнего рекорда, а теперь ниже нового.
        var overtaken = await scores
            .Include(x => x.User)
            .Where(x => x.ChatId == chatId && x.UserId != userId && x.Score < score)
            .Where(x => previousScore == null || x.Score >= previousScore)
            .OrderByDescending(x => x.Score)
            .ToListAsync(cancellationToken);

        if (existing is null)
        {
            var entity = create();
            entity.UserId = userId;
            entity.ChatId = chatId;
            entity.Score = score;
            scores.Add(entity);
        }
        else
        {
            existing.Score = score;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (existing is null && score <= 0)
        {
            return null;
        }

        var top = overtaken.Count == 0
            ? []
            : await scores
                .Include(x => x.User)
                .Where(x => x.ChatId == chatId)
                .OrderByDescending(x => x.Score)
                .Take(TopSize)
                .ToListAsync(cancellationToken);

        return new GameRecordResult<TEntity>(overtaken, top);
    }
}
