# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build entire solution
dotnet build Saturn.Telegram.sln

# Run locally (requires .env values as env vars)
dotnet run --project Saturn.Telegram.Bot/Saturn.Telegram.Bot.csproj
dotnet run --project Saturn.Telegram.Api/Saturn.Telegram.Api.csproj

# Run via Docker Compose (recommended — starts bot + API + PostgreSQL)
docker compose up
```

Database migrations are applied automatically at startup — no manual migration step needed.

## Architecture

Four projects:

- **Saturn.Telegram.Lib** — core abstractions: `IOperation`, `OperationManager`, `ICooldownService`, `TelegramHostedService`. This is where the operation dispatch loop lives.
- **Saturn.Telegram.Bot** — concrete `IOperation` implementations (message handlers) and bot-specific services (FFmpeg setup, yt-dlp setup, message saving).
- **Saturn.Telegram.Api** — ASP.NET Core REST API exposing stats, AI agents, and chat data for a web frontend. Validates callers via `TelegramInitDataMiddleware`.
- **Saturn.Telegram.Db** — EF Core 10 + PostgreSQL (Npgsql). Entities: `MessageEntity`, `UserEntity`, `ChatEntity`, `AiAgentEntity`, `OperationCallEntity`. Uses snake_case naming convention and `IDbContextFactory<SaturnContext>` throughout.

## Operation Pattern

Every bot command is an `IOperation` (in `Saturn.Telegram.Lib/Operation/IOperation.cs`). To add a new command:

1. Create a class implementing `IOperation` under `Saturn.Telegram.Bot/Operations/`.
2. Implement `Validate(Message)` — return true if this operation should handle the message.
3. Implement `OnMessageAsync(Message, CancellationToken)` — perform the action.
4. Register it in the DI container; `OperationManager` discovers all `IOperation` instances automatically.

Use `[ChatOnly]` attribute to restrict an operation to group chats. Use `ICooldownService` to rate-limit repeated calls.

After adding a new operation, document it in `Saturn.Telegram.Bot/Help.md` in Russian — this file is sent to users in response to the `помощь` command (via `HelpOperation`). It is copied to the output directory at build time. Add the new command to the relevant section (ИИ, Статистика, Развлечения, Прочее) using the same format: `` `команда` — описание ``.

## Environment Variables

Configured via `.env` (picked up by Docker Compose):

| Variable | Purpose |
|---|---|
| `BOT_TOKEN` | Telegram bot token |
| `CONNECTION_STRING` | PostgreSQL connection string |
| `POSTGRES_PASSWORD` | DB password |
| `LOG_CHAT_ID` | Telegram chat ID for error logs |
| `IMAGE_MANIPULATION_SERVICE_URL` | External image service endpoint |
| `*OperationEnabled` | Feature flags per operation (e.g. `StatisticsOperationEnabled`) |

## Key Dependencies

- `Telegram.Bot` 22.9.6 — bot API client
- `OpenAI` 2.10.0 — GPT integration
- `Magick.NET-Q16-AnyCPU` 14.11.1 — image manipulation
- `Xabe.FFmpeg` + `YoutubeDLSharp` — media download/processing
- EF Core 10 + Npgsql 10 — data access

## Deployment

CI/CD via GitHub Actions (`.github/workflows/deploy.yml`): push to `master` builds and pushes Docker images to Docker Hub (`midianok/saturn`, `midianok/saturn-api`), then SSHes into the server and runs `docker compose up`.
