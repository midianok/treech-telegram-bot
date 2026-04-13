using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpGet("weekly")]
    public async Task<IActionResult> GetWeekly([FromQuery] long chatId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var monday = GetMondayDate();

        var users = await db.Messages
            .Where(x => x.ChatId == chatId && x.MessageDate >= monday && x.MessageDate < DateTime.Now)
            .GroupBy(x => x.UserId)
            .Select(x => new
            {
                Name = (x.First().User!.FirstName + " " + x.First().User!.LastName).Trim(),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Ok(new { users });
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthly([FromQuery] long chatId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var users = await db.Messages
            .Where(x => x.ChatId == chatId && x.MessageDate >= firstDayOfMonth && x.MessageDate < DateTime.Now)
            .GroupBy(x => x.UserId)
            .Select(x => new
            {
                Name = (x.First().User!.FirstName + " " + x.First().User!.LastName).Trim(),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Ok(new { users });
    }

    private static DateTime GetMondayDate() =>
        DateTime.Now.DayOfWeek switch
        {
            DayOfWeek.Monday    => DateTime.Now.Date,
            DayOfWeek.Tuesday   => DateTime.Now.AddDays(-1).Date,
            DayOfWeek.Wednesday => DateTime.Now.AddDays(-2).Date,
            DayOfWeek.Thursday  => DateTime.Now.AddDays(-3).Date,
            DayOfWeek.Friday    => DateTime.Now.AddDays(-4).Date,
            DayOfWeek.Saturday  => DateTime.Now.AddDays(-5).Date,
            DayOfWeek.Sunday    => DateTime.Now.AddDays(-6).Date,
            _                   => throw new ArgumentOutOfRangeException()
        };
}
