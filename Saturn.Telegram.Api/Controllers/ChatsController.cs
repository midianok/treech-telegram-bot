using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/chats")]
public class ChatsController(IDbContextFactory<SaturnContext> contextFactory) : ControllerBase
{
    [HttpGet("{chatId:long}/ai-agent")]
    public async Task<ActionResult<AiAgentDto?>> GetAiAgent(long chatId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var chat = await db.Chats
            .Include(x => x.AiAgent)
            .FirstOrDefaultAsync(x => x.Id == chatId, cancellationToken);

        if (chat is null)
        {
            return NotFound("Chat not found");
        }

        if (chat.AiAgent is null)
        {
            return Ok(null);
        }

        return new AiAgentDto(chat.AiAgent.Id, chat.AiAgent.Name, chat.AiAgent.Prompt);
    }

    [HttpPut("{chatId:long}/ai-agent")]
    public async Task<ActionResult> SetAiAgent(long chatId, [FromBody] SetChatAiAgentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var chat = await db.Chats.FindAsync([chatId], cancellationToken);
        if (chat == null)
        {
            return NotFound("Chat not found");
        }

        var agentExists = await db.AiAgents.AnyAsync(x => x.Id == request.AgentId, cancellationToken);
        if (!agentExists)
        {
            return NotFound("AI agent not found");
        }

        chat.AiAgentId = request.AgentId;
        db.Chats.Update(chat);
        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_notify('chat_invalidation', {0})", chatId.ToString());

        return NoContent();
    }
}