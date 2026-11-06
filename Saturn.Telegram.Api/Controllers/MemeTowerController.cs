using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Api.Services;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Telegram.Api.Controllers;

/// <summary>
/// Таблица лидеров игры «Мемный трич» (башня из мемов). Лучший счёт хранится на пару пользователь + чат.
/// </summary>
[ApiController]
[Route("api/meme-tower")]
public class MemeTowerController : ApiControllerBase
{
    /// <summary>Short name Mini App в BotFather: https://t.me/{bot}/{MiniAppShortName}.</summary>
    public const string MiniAppShortName = "memnytrich";

    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly ChatMembershipService _membershipService;
    private readonly string? _botUsername;

    public MemeTowerController(IDbContextFactory<SaturnContext> contextFactory,
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
    public async Task<ActionResult> AddScore([FromBody] AddMemeTowerScoreRequest request, CancellationToken cancellationToken)
    {
        if (request.Score < 0)
            return BadRequest("Score must not be negative");

        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value != request.UserId)
            return Forbid();

        if (!await _membershipService.IsMemberAsync(request.ChatId, currentUserId, cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
        {
            return NotFound($"User {request.UserId} not found");
        }

        var existing = await db.MemeTowerScores.FindAsync([request.UserId, request.ChatId], cancellationToken);
        if (existing is null)
        {
            db.MemeTowerScores.Add(new MemeTowerScoreEntity
            {
                UserId = request.UserId,
                ChatId = request.ChatId,
                Score = request.Score
            });
        }
        else if (request.Score > existing.Score)
        {
            existing.Score = request.Score;
        }

        await db.SaveChangesAsync(cancellationToken);

        var keyboard = string.IsNullOrEmpty(_botUsername)
            ? null
            : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(
                "Мемный трич",
                $"https://t.me/{_botUsername}/{MiniAppShortName}?startapp={request.ChatId}"));

        await _botClient.SendMessage(
            request.ChatId,
            $"{user.GetSilentHtmlLink()} построил башню из {request.Score} мемов в Мемном триче!",
            parseMode: ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpGet("score/{userId:long}")]
    public async Task<ActionResult<MemeTowerLeaderboardEntryDto>> GetScore(
        long userId,
        [FromQuery] long chatId,
        CancellationToken cancellationToken)
    {
        if (!await _membershipService.IsMemberAsync(chatId, GetCurrentUserId(), cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.MemeTowerScores
            .Include(x => x.User)
            .Where(x => x.UserId == userId && x.ChatId == chatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return new MemeTowerLeaderboardEntryDto(entity.UserId, entity.User!.GetDisplayName(), entity.Score);
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<IEnumerable<MemeTowerLeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] long chatId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await _membershipService.IsMemberAsync(chatId, GetCurrentUserId(), cancellationToken))
            return Forbid();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.MemeTowerScores
            .Include(x => x.User)
            .Where(x => x.ChatId == chatId)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);

        return Ok(entities.Select(x => new MemeTowerLeaderboardEntryDto(x.UserId, x.User!.GetDisplayName(), x.Score)));
    }
}
