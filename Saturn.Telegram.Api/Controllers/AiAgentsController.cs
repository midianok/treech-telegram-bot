using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/ai-agents")]
public class AiAgentsController(IDbContextFactory<SaturnContext> contextFactory, IChatCachedRepository chatCachedRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var agents = await db.AiAgents
            .Select(x => new AiAgentDto(x.Id, x.Name, x.Prompt))
            .ToListAsync(cancellationToken);

        return Ok(agents);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAiAgentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var agent = new AiAgentEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Prompt = request.Prompt
        };

        db.AiAgents.Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new AiAgentDto(agent.Id, agent.Name, agent.Prompt));
    }

    [HttpPut("{id:guid}/prompt")]
    public async Task<IActionResult> UpdatePrompt(Guid id, [FromBody] UpdateAiAgentPromptRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var agent = await db.AiAgents.FindAsync([id], cancellationToken);
        if (agent is null)
            return NotFound();

        agent.Prompt = request.Prompt;
        await db.SaveChangesAsync(cancellationToken);
        await chatCachedRepository.InvalidateByAgentAsync(id, cancellationToken);

        return Ok(new AiAgentDto(agent.Id, agent.Name, agent.Prompt));
    }
}