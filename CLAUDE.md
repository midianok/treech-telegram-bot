/# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Treech is a feature-rich Telegram bot for group chats built on .NET 9.0. It provides AI-powered features (via X.AI/Grok), message statistics, fun interactions, and persists all messages to PostgreSQL.

## Commands

```bash
# Build
dotnet build Saturn.Telegram.sln

# Run locally (requires .env variables set)
dotnet run --project Saturn.Telegram.Bot

# Docker (full stack: bot + postgres + image-manipulation-service)
docker-compose up -d

# EF Core migrations
dotnet ef database update --project Saturn.Telegram.Db
dotnet ef migrations add <MigrationName> --project Saturn.Telegram.Db
```

There are no automated tests in this project.

## Architecture

### Three-Project Solution

- **Saturn.Telegram.Bot** — Main executable; all feature operations, services, external clients, DI wiring
- **Saturn.Telegram.Lib** — Reusable bot framework: `IOperation` interface, `OperationManager`, `TelegramHostedService`, `CooldownService`, logging
- **Saturn.Telegram.Db** — EF Core + PostgreSQL layer: entities, repositories, migrations, `SaturnContext`

### IOperation Plugin Pattern

All bot features implement `IOperation`:

```csharp
public interface IOperation
{
    bool Validate(Message msg, UpdateType type);       // Should this operation handle the message?
    Task OnMessageAsync(Message msg, UpdateType type); // Handle it
    Task OnUpdateAsync(Update update);                 // Handle non-message updates
}
```

`OperationManager` discovers all `IOperation` implementations via assembly reflection at startup, then routes each incoming Telegram message through them: calls `Validate()`, checks cooldown via `CooldownService`, then calls `OnMessageAsync()`. **To add a new feature, implement `IOperation` and register it in DI — it is auto-discovered.**

Use `[Cooldown(seconds)]` attribute on an operation class to rate-limit it per user.

### Operation Categories (Saturn.Telegram.Bot/Operations/)

- **Ai/** — Chat generation (`/` prefix), image generation (`покажи`), image editing (`отредактируй`/`измени`), image description (`нука`)
- **Statistics/** — User stats, top talkers, favorite stickers, all-time stats
- **FunnyStaff/** — Roll (`на дабл`), image distortion (`жмыхни`), who-today picker
- **Infrastructure/** — `SaveMessageOperation` persists every message to DB; `HelpOperation` responds to `помощь` with bot usage guide

> **Important:** When adding a new operation, update `Saturn.Telegram.Bot/help.md` to document the new trigger and what it does. This file is read once at startup and served verbatim by `HelpOperation`.

### Data Flow

```
Telegram → TelegramHostedService (polling)
         → OperationManager (iterates registered IOperations)
         → Validate() → CooldownService → OnMessageAsync()
         → Error logged to Telegram chat (LOG_CHAT_ID)
```

AI chat operations reconstruct conversation context by following reply-to chains via `MessageRepository.GetMessageChainAsync()`.

### Key Services

| Service | Role |
|---|---|
| `SaveMessageService` | Persists messages + user/chat metadata; uses `SemaphoreSlim` for thread safety |
| `CooldownService` | Per-user per-operation rate limiting (in-memory) |
| `ChatClient` / `ImageClient` | OpenAI SDK pointed at X.AI (Grok) endpoints |
| `XaiImageEditClient` | Custom HTTP client for X.AI image editing API |
| `IImageManipulationServiceClient` | HTTP client for external image distortion service |
| `IChatCachedRepository` | Chat lookup with 30-hour memory cache (used for AI agent prompts) |

### Database (EF Core + PostgreSQL)

Entities: `UserEntity`, `ChatEntity`, `MessageEntity`, `AiAgentEntity`. Snake_case naming via `EFCore.NamingConventions`. DbContext is accessed via `IDbContextFactory<SaturnContext>` (transient pattern). AI agents are per-chat personality prompts stored in DB.

### Configuration

All config via environment variables (`.env` file):
- `BOT_TOKEN` — Telegram bot token
- `CONNECTION_STRING` — PostgreSQL connection string  
- `CHAT_GENERATION_API_KEY` / `IMAGE_GENERATION_API_KEY` — X.AI API keys
- `INVOKE_COMMAND` — Prefix triggering AI chat (e.g. `/`)
- `LOG_CHAT_ID` — Telegram chat ID for error/operation logs
- Feature flags: `ImageDistortionOperationEnabled`, `SaveMessageOperationEnabled`, `ShowChatLinkOperationEnabled`
- `IMAGE_MANIPULATION_SERVICE_URL` — URL for image distortion service

### Logging

A custom `TelegramLoggerProvider` sends application logs directly to `LOG_CHAT_ID`. Configured in `Program.cs` with filters to suppress noisy `Microsoft.*` logs below Error level.

### DI Registration

Services are registered via extension methods:
- `Saturn.Bot.Service.Extensions.ServiceCollectionsExtensions` (bot-specific)
- `Saturn.Telegram.Lib.Extensions.ServiceCollectionsExtensions` (framework)
- `Saturn.Telegram.Db.Extensions.ServiceCollectionsExtensions` (database)