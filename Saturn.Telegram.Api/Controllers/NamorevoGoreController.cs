using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Api.Services;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/namorevo-gore")]
public class NamorevoGoreController : ApiControllerBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly ChatMembershipService _membershipService;
    private readonly string? _botUsername;

    public NamorevoGoreController(IDbContextFactory<SaturnContext> contextFactory,
        ITelegramBotClient botClient,
        ChatMembershipService membershipService,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _botClient = botClient;
        _membershipService = membershipService;
        _botUsername = configuration["BOT_USERNAME"];
    }

    [HttpPost("score")]
    public async Task<ActionResult> AddScore([FromBody] AddNamorevoGoreScoreRequest request, CancellationToken cancellationToken)
    {
        if (!await _membershipService.IsMemberAsync(request.ChatId, GetCurrentUserId(), cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
        {
            return NotFound($"User {request.UserId} not found");
        }

        var existing = await db.NamorevoGoreScores.FindAsync([request.UserId, request.ChatId], cancellationToken);
        if (existing is null)
        {
            db.NamorevoGoreScores.Add(new NamorevoGoreScoreEntity
            {
                UserId = request.UserId,
                ChatId = request.ChatId,
                Score = request.Score
            });
        }
        else if (request.Score > existing.Score)
        {
            db.NamorevoGoreScores.Update(new NamorevoGoreScoreEntity
            {
                UserId = existing.UserId,
                ChatId = existing.ChatId,
                Score = request.Score
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(_botUsername))
        {
            var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Наморево горе", $"https://t.me/{_botUsername}/namorevogore?startapp={request.ChatId}"));
            var userName = user.GetDisplayName();
            await _botClient.SendMessage(
                request.ChatId,
                $"{userName} набрал {request.Score} очков в Наморево Горе!",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        return Ok();
    }

    [HttpGet("score/{userId:long}")]
    public async Task<ActionResult<NamorevoGoreLeaderboardEntryDto>> GetScore(
        long userId,
        [FromQuery] long chatId,
        CancellationToken cancellationToken)
    {
        if (!await _membershipService.IsMemberAsync(chatId, GetCurrentUserId(), cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.NamorevoGoreScores
            .Include(x => x.User)
            .Where(x => x.UserId == userId && x.ChatId == chatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return new NamorevoGoreLeaderboardEntryDto(entity.UserId, entity.User!.GetDisplayName(), entity.Score);
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<IEnumerable<NamorevoGoreLeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] long chatId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await _membershipService.IsMemberAsync(chatId, GetCurrentUserId(), cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.NamorevoGoreScores
            .Include(x => x.User)
            .Where(x => x.ChatId == chatId)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(entities.Select(x => new NamorevoGoreLeaderboardEntryDto(x.UserId, x.User!.GetDisplayName(), x.Score)));
    }

}
