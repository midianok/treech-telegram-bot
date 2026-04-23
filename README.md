# Saturn Telegram Bot

Telegram-бот для групповых чатов с поддержкой ИИ-функций, статистики, развлечений и автоматической загрузки медиа. Написан на .NET 10, задеплоен через Docker Compose.

## Возможности

### 🤖 ИИ
| Команда | Описание |
|---|---|
| `трич [вопрос]` | Задать вопрос ИИ (GPT) |
| Ответ на сообщение бота | Продолжить диалог |
| `покажи [описание]` | Сгенерировать изображение |
| `измени описание` | Отредактировать фото (ответ на фото или фото с подписью) |
| `нука` | Описать фото |
| `портрет` | Психологический портрет по истории сообщений |
| `саммари` | Саммари чата за сегодня |
| `саммари 2025-01-31` | Саммари за конкретный день |

### 📊 Статистика
| Команда | Описание |
|---|---|
| `стата` | Количество сообщений по типам |
| `топ стата` | Топ-10 активных за неделю |
| `вся стата` | Топ-10 активных за всё время |
| `любимый стикер` | Любимый стикер пользователя |

### 🎉 Развлечения
| Команда | Описание |
|---|---|
| `на дабл` | Кинуть кубики (число от 10 до 99) |
| `жмыхни` | Исказить медиа (ответ на фото, видео или гифку) |
| `кто сегодня [роль]` | Случайный участник из активных сегодня |
| `найти [запрос]` | Скачать трек с YouTube |

### ⚙️ Прочее
- `бот` — открыть веб-приложение
- `помощь` — список команд
- Ссылки TikTok, Instagram Reels и YouTube Shorts скачиваются **автоматически**

## Архитектура

```
Saturn.Telegram.Lib   — ядро: IOperation, OperationManager, ICooldownService, TelegramHostedService
Saturn.Telegram.Bot   — реализации команд, FFmpeg/yt-dlp, сохранение сообщений
Saturn.Telegram.Api   — REST API для веб-фронтенда (ASP.NET Core), авторизация через Telegram InitData
Saturn.Telegram.Db    — EF Core 10 + PostgreSQL (Npgsql), snake_case, IDbContextFactory
```

## Быстрый старт

### Требования
- Docker & Docker Compose
- Telegram Bot Token (`@BotFather`)
- OpenAI API Key

### Запуск

1. Создайте файл `.env` в корне проекта:

```env
BOT_TOKEN=your_telegram_bot_token
CONNECTION_STRING=Host=db;Port=5432;Database=postgres;Username=postgres;Password=your_password
POSTGRES_PASSWORD=your_password
LOG_CHAT_ID=your_chat_id_for_error_logs
IMAGE_MANIPULATION_SERVICE_URL=http://your-image-service
# Feature flags (опционально)
StatisticsOperationEnabled=true
```

2. Запустите:

```bash
docker compose up
```

Миграции БД применяются автоматически при старте.

### Локальный запуск (без Docker)

```bash
dotnet build Saturn.Telegram.sln
dotnet run --project Saturn.Telegram.Bot/Saturn.Telegram.Bot.csproj
dotnet run --project Saturn.Telegram.Api/Saturn.Telegram.Api.csproj
```

## Деплой

CI/CD настроен через GitHub Actions: при пуше в `master` собираются Docker-образы и публикуются на Docker Hub (`midianok/saturn`, `midianok/saturn-api`), затем по SSH выполняется `docker compose up` на сервере.

Необходимые секреты в GitHub: `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`, `SSH_HOST`, `SSH_USER`, `SSH_PASSWORD`, `DEPLOY_PATH`.

## Стек

- [.NET 10](https://dotnet.microsoft.com/)
- [Telegram.Bot 22.9.6](https://github.com/TelegramBots/Telegram.Bot)
- [OpenAI .NET SDK 2.10.0](https://github.com/openai/openai-dotnet)
- [EF Core 10](https://learn.microsoft.com/en-us/ef/core/) + [Npgsql 10](https://www.npgsql.org/)
- [Magick.NET 14.11.1](https://github.com/dlemstra/Magick.NET) — обработка изображений
- [Xabe.FFmpeg](https://github.com/tomaszzmuda/Xabe.FFmpeg) + [YoutubeDLSharp](https://github.com/Bluegrams/YoutubeDLSharp) — медиа
- PostgreSQL 17
