using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/image-prompts")]
public class ImagePromptsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ImagePromptDto>> GetAll(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.ImagePrompts
            .Select(x => new ImagePromptDto(x.Id, x.Name, x.Keywords, x.Prompt))
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ImagePromptDto>> Create([FromBody] UpdateImagePromptRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new ImagePromptEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Keywords = request.Keywords,
            Prompt = request.Prompt
        };

        db.ImagePrompts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_notify('image_prompt_invalidation', '')", cancellationToken: cancellationToken);

        return new ImagePromptDto(entity.Id, entity.Name, entity.Keywords, entity.Prompt);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ImagePromptDto>> Update(Guid id, [FromBody] UpdateImagePromptRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.ImagePrompts.FindAsync([id], cancellationToken);
        if (entity is null)
            return NotFound();

        entity.Name = request.Name;
        entity.Keywords = request.Keywords;
        entity.Prompt = request.Prompt;
        db.ImagePrompts.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_notify('image_prompt_invalidation', '')", cancellationToken: cancellationToken);

        return new ImagePromptDto(entity.Id, entity.Name, entity.Keywords, entity.Prompt);
    }
}
