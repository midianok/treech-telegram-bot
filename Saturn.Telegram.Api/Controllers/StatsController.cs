using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpGet("top")]
    public async Task<IActionResult> GetTop(
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

        return Ok(new { users });
    }
}
