using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Api.Dto;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/chats")]
public class ChatsController(IDbContextFactory<SaturnContext> contextFactory, IChatCachedRepository chatCachedRepository) : ControllerBase
{
    [HttpGet("{chatId:long}/ai-agent")]
    public async Task<IActionResult> GetAiAgent(long chatId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var chat = await db.Chats
            .Include(x => x.AiAgent)
            .FirstOrDefaultAsync(x => x.Id == chatId, cancellationToken);

        if (chat is null)
            return NotFound("Chat not found");

        if (chat.AiAgent is null)
            return Ok(null);

        return Ok(new AiAgentDto(chat.AiAgent.Id, chat.AiAgent.Name, chat.AiAgent.Prompt));
    }

    [HttpPut("{chatId:long}/ai-agent")]
    public async Task<IActionResult> SetAiAgent(long chatId, [FromBody] SetChatAiAgentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var chatExists = await db.Chats.AnyAsync(x => x.Id == chatId, cancellationToken);
        if (!chatExists)
            return NotFound("Chat not found");

        var agentExists = await db.AiAgents.AnyAsync(x => x.Id == request.AgentId, cancellationToken);
        if (!agentExists)
            return NotFound("AI agent not found");

        await chatCachedRepository.SetAiAgentAsync(chatId, request.AgentId, cancellationToken);

        return NoContent();
    }
}