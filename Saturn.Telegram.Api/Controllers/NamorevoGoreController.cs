using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
        else if (request.Score > existing.Score)
        {
            db.NamorevoGoreScores.Update(new NamorevoGoreScoreEntity
            {
                UserId = existing.UserId,
                Score = request.Score
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("Наморево горе", $"https://t.me/TreechBot/namorevogore?startapp={request.ChatId}"));
        
        var userName = FormatUserName(user);
        await botClient.SendMessage(
            request.ChatId,
            $"{userName} набрал {request.Score} очков в Наморево Горе!",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpGet("score/{userId:long}")]
    public async Task<ActionResult<NamorevoGoreLeaderboardEntryDto>> GetScore(long userId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.NamorevoGoreScores
            .Include(x => x.User)
            .Where(x => x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return new NamorevoGoreLeaderboardEntryDto(entity.UserId, FormatUserName(entity.User!), entity.Score);
    }

    [HttpGet("leaderboard")]
    public async Task<IEnumerable<NamorevoGoreLeaderboardEntryDto>> GetLeaderboard(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.NamorevoGoreScores
            .Include(x => x.User)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(x => new NamorevoGoreLeaderboardEntryDto(x.UserId, FormatUserName(x.User!), x.Score));
    }

    private static string FormatUserName(UserEntity user)
    {
        var fullName = (user.FirstName + " " + user.LastName).Trim();
        if (!string.IsNullOrEmpty(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return $"@{user.Username}";
        }

        return user.Id.ToString();
    }
}
