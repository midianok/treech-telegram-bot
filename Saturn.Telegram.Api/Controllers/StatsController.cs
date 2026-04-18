using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpGet("message-count")]
    public async Task<MessageCountDto> GetCount(
        [FromQuery] long chatId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Messages.Where(x => x.ChatId == chatId);

        if (dateFrom.HasValue)
            query = query.Where(x => x.MessageDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.MessageDate < dateTo.Value);

        var count = await query.CountAsync(cancellationToken);

        return new MessageCountDto(count);
    }

    [HttpGet("top-users")]
    public async Task<IEnumerable<UserMessageCountDto>> GetTop(
        [FromQuery] long chatId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Messages.Where(x => x.ChatId == chatId && x.UserId != 5990847351);

        if (dateFrom.HasValue)
            query = query.Where(x => x.MessageDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.MessageDate < dateTo.Value);

        var users = await query
            .GroupBy(x => x.UserId)
            .Select(x => new
            {
                Name = (x.First().User!.FirstName + " " + x.First().User!.LastName).Trim(),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return users.Select(x => new UserMessageCountDto(x.Name, x.Count));
    }
}
