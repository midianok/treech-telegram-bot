using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLogRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new LogEntity
        {
            Source = request.Source,
            Level = request.Level,
            Message = request.Message,
            Data = request.Data.GetRawText(),
            CreatedAt = DateTime.UtcNow
        };

        db.Logs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}
