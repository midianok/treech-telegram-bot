using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Telegram.Bot;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/namorevo-gore")]
public class NamorevoGoreController(
    IDbContextFactory<SaturnContext> contextFactory,
    ITelegramBotClient botClient) : ControllerBase
{
    [HttpPost("score")]
    public async Task<ActionResult> AddScore([FromBody] AddNamorevoGoreScoreRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
        {
            return NotFound($"User {request.UserId} not found");
        }

        var existing = await db.NamorevoGoreScores.FindAsync([request.UserId], cancellationToken);
        if (existing is null)
        {
            db.NamorevoGoreScores.Add(new NamorevoGoreScoreEntity
            {
                UserId = request.UserId,
                Score = request.Score
            });
        }
        else
        {
            db.NamorevoGoreScores.Update(new NamorevoGoreScoreEntity
            {
                UserId = existing.UserId,
                Score = existing.Score + request.Score
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var userName = FormatUserName(user);
        await botClient.SendMessage(
            request.ChatId,
            $"{userName} заработал {request.Score} очков в Наморево Горе! Всего: {(existing?.Score ?? 0) + request.Score}",
            cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpGet("score/{userId:long}")]
    public async Task<ActionResult<NamorevoGoreLeaderboardEntryDto>> GetScore(long userId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entry = await db.NamorevoGoreScores
            .Include(x => x.User)
            .Where(x => x.UserId == userId)
            .Select(x => new NamorevoGoreLeaderboardEntryDto(
                x.UserId,
                (x.User!.FirstName + " " + x.User.LastName).Trim(),
                x.Score))
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        return entry;
    }

    [HttpGet("leaderboard")]
    public async Task<IEnumerable<NamorevoGoreLeaderboardEntryDto>> GetLeaderboard(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entries = await db.NamorevoGoreScores
            .Include(x => x.User)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => new NamorevoGoreLeaderboardEntryDto(
                x.UserId,
                (x.User!.FirstName + " " + x.User.LastName).Trim(),
                x.Score))
            .ToListAsync(cancellationToken);

        return entries;
    }

    private static string FormatUserName(UserEntity user)
    {
        var name = (user.FirstName + " " + user.LastName).Trim();
        return string.IsNullOrEmpty(name) ? user.Username ?? user.Id.ToString() : name;
    }
}
