# Аудит перед прод-релизом

Дата: 2026-05-23
Ревьюер: Senior .NET review
Объём: 4 проекта (Bot / Api / Lib / Db), ~117 .cs файлов, миграции и инфраструктура.

Все секреты, попавшие в репозиторий, отозваны до начала ревью — пункты про утечку токенов из истории git выведены в отдельный раздел (см. §1) только как организационное напоминание.

Серьёзность: 🔴 критично / 🟠 высоко / 🟡 средне / 🔵 низко / ⚪ косметика.

---

## 1. Безопасность

### ✅ 1.1 Файл `.env` остаётся под git-трекингом
```
git ls-files | grep .env  →  .env
```
`.gitignore` содержит `.env`, но файл уже добавлен ранее — `.gitignore` на отслеживаемые файлы не действует.

Действия:
1. `git rm --cached .env`, коммит.
2. (Опционально) переписать историю через `git filter-repo --path .env --invert-paths`.
3. Добавить pre-commit хук (`gitleaks` / `detect-secrets`) либо включить GitHub Push Protection, чтобы исключить повторение.

### ✅ 1.2 `TelegramInitDataMiddleware` не проверяет `auth_date`
`Saturn.Telegram.Api/Middleware/TelegramInitDataMiddleware.cs:8-18` — HMAC-подпись валидируется корректно, но возраст `init_data` не проверяется. Любой однажды перехваченный `X-Telegram-Init-Data` валиден бесконечно. Это противоречит официальной рекомендации Telegram (TTL обычно 24 ч).

```csharp
// добавить в Validate():
if (!pairs.TryGetValue("auth_date", out var authDateStr)
    || !long.TryParse(authDateStr, out var unix)
    || DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix) > TimeSpan.FromHours(24))
{
    return false;
}
```

Дополнительно: сравнение хэшей сделать константно-временным:
```csharp
return CryptographicOperations.FixedTimeEquals(
    Convert.FromHexString(expectedHash),
    Convert.FromHexString(hash));
```

### ✅ 1.3 Авторизация API применяется только в Production
`Saturn.Telegram.Api/Program.cs:53-57`:
```csharp
if (app.Environment.IsProduction())
{
    app.UseCors();
    app.UseMiddleware<TelegramInitDataMiddleware>();
}
```
В Development всё API открыто (без CORS, без проверки initData). В `docker-compose.yaml` не задана `ASPNETCORE_ENVIRONMENT` → дефолт `Production` спасает, но это хрупко: достаточно случайно переменной окружения с другим значением, и контур API окажется без авторизации.

Действия:
- Явно задать `ASPNETCORE_ENVIRONMENT=Production` в `docker-compose.yaml`.
- Middleware применять всегда; в dev отключать осознанно отдельным флагом (`AuthEnabled=false`), не зависящим от окружения.

### ✅ 1.4 Контроллеры доверяют `chatId` из тела/query без проверки членства
Например, `NamorevoGoreController.AddScore` (`Saturn.Telegram.Api/Controllers/NamorevoGoreController.cs:28-74`):
- получает `request.ChatId` от клиента;
- пишет запись в БД;
- отправляет сообщение `{userName} набрал {request.Score} очков...` в этот чат.

Аналогично `ChatsController.SetAiAgent`, `StatsController.GetTop`, `StatsController.GetOperationCalls`. После прохождения `TelegramInitDataMiddleware` мы знаем `user.id` из initData, но **не проверяем**, что этот пользователь действительно состоит в `chatId`. Последствия:
- спам сообщениями «N набрал M очков» в любой чат, где есть бот;
- смена AI-агента в чужом чате;
- чтение статистики чужих чатов.

Действия (любое из):
- проверка `await botClient.GetChatMember(chatId, userId)` при чувствительных операциях (с кэшем);
- серверная подпись `chatId` при выдаче ссылки `t.me/...?startapp=<signed>`;
- хранение и проверка whitelist `(userId, chatId)`.

### 🚫 1.5 SSH-деплой по паролю [игнорируется]
`.github/workflows/deploy.yml` — `appleboy/ssh-action` использует `SSH_PASSWORD`. Переключиться на key-based auth (`SSH_KEY`).

### 🚫 1.6 Подавление security-warning'ов NuGet [игнорируется]
`Saturn.Telegram.Bot/Saturn.Telegram.Bot.csproj`:
```xml
<NoWarn>$(NoWarn);NU1901;NU1902;NU1903;NU1904</NoWarn>
```
Это коды о CVE в транзитивных зависимостях. Убрать; добавить в CI `dotnet list package --vulnerable --include-transitive`.

### 🚫 1.7 PostgreSQL открыт наружу [игнорируется]
`docker-compose.yaml`: `ports: ["15432:5432"]` — БД доступна из любого внешнего адреса, если файрвол сервера открыт. На проде либо удалить, либо биндить на `127.0.0.1:15432:5432`.

### 🚫 1.8 Сырая интерполяция в `pg_notify` через `ExecuteSqlRawAsync` [игнорируется]
`ChatsController.cs:54`, `AiAgentsController.cs:56,79`, `ImagePromptsController.cs:38,57`:
```csharp
await db.Database.ExecuteSqlRawAsync("SELECT pg_notify('chat_invalidation', {0})", chatId.ToString());
```
Здесь параметризовано — формально безопасно, но `ExecuteSqlInterpolatedAsync` или `NpgsqlParameter` единообразнее и устойчивее к ошибкам.

---

## 2. Документация vs реальность

### ✅ 2.1 README.md — несоответствия

| README | Факт |
|---|---|
| «OpenAI API Key», «GPT» | xAI Grok, endpoint `https://api.x.ai/v1`, модель `grok-4-1-fast-non-reasoning` |
| `Telegram.Bot 22.9.6` | `22.10.0.1` |
| `Magick.NET 14.11.1` | `14.13.1` |
| Docker Hub `midianok/saturn`, секреты `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` | GHCR `ghcr.io/midianok/...`, используется `GITHUB_TOKEN` |
| «YouTube Shorts скачиваются автоматически» | `VideoUrlRegex` (`VideoDownloadOperation.cs:108-111`) ловит только `tiktok.com` и `instagram.com/reel/...`, YouTube не покрывается |
| Команда `найти [запрос]` — «скачать трек с YouTube» | Реализации нет (поиск по коду пуст) |
| Список команд | Не упомянуты: `топ слов`, `карма`, `спасибо`/`+`/`фу`/`-`, `наморево горе`, `оживи` (есть в `Help.md`) |

### ✅ 2.2 CLAUDE.md / AGENTS.md
- `IMAGE_MANIPULATION_SERVICE_URL` упоминается как переменная — **в коде не используется**, мёртвая конфигурация. Удалить из документации и `.env`.
- Сигнатура `OnMessageAsync(Message, CancellationToken)` указана неверно — реально `OnMessageAsync(Message, UpdateType)`, **CancellationToken не передаётся вообще** (см. §3.16).
- Не упомянут `BOT_USERNAME` в README (есть в CLAUDE.md), `INVOKE_COMMAND`, `EASTER_EGG_USERNAME`, ключи xAI.

### ✅ 2.3 `.env.example` пробелы
Текущий файл:
```
BOT_TOKEN=, BOT_USERNAME=, LOG_CHAT_ID=, ADMIN_USERNAME=,
CONNECTION_STRING=, POSTGRES_PASSWORD=,
CHAT_GENERATION_API_KEY=, IMAGE_GENERATION_API_KEY=, IMAGE_EDIT_API_KEY=,
YOUTUBE_COOKIES_PATH=, INSTAGRAM_COOKIES_PATH=, PATH_BASE=
```

Отсутствуют:
- 🔴 **`INVOKE_COMMAND`** — обязательная (`GetSectionOrThrow`), без неё бот не стартует на чистом окружении.
- 🟡 `EASTER_EGG_USERNAME` — опциональная, но фича без неё молча выключена.
- 🟡 Feature-флаги `*OperationEnabled` (см. файл `.env`: `ImageDistortionOperationEnabled` и др.). По grep — нигде в коде не читаются. Либо удалить из `.env`, либо реализовать и описать.

### ✅ 2.4 Текущий `.env` содержит мёртвую конфигурацию
- `IMAGE_MANIPULATION_SERVICE_URL` — не используется кодом.
- `*OperationEnabled` — не читаются. Если фичи опциональны, нужен реальный механизм (см. §3.21).

---

## 3. Архитектура и качество кода

### ✅ 3.1 Sync-over-async в `ChatGenerationOperation`
`Saturn.Telegram.Bot/Operations/Ai/ChatGenerationOperation.cs:155`:
```csharp
var bot = _memoryCache.GetOrCreate($"...", async _ => await _telegramBotClient.GetMe())
                     ?.GetAwaiter().GetResult();
```
Проблемы:
- `GetOrCreate` кэширует `Task<User>`, `.GetResult()` блокирует поток thread pool — starvation под нагрузкой.
- Если `GetMe()` бросает — failed Task закэширован навсегда; каждый последующий `Validate` ребрасывает то же исключение.

Решение: вынести `Me` в singleton, инициализируемый один раз на старте, либо переписать на `async Validate`. Текущий `IOperation` `Validate` синхронный — потребует изменения интерфейса.

### 🚫 3.2 `OperationManager` не ставит cooldown при исключении операции [игнорируется]
`Saturn.Telegram.Lib/OperationManager.cs:65-87` — последовательность:
```csharp
try {
    await operation.OnMessageAsync(msg, type);
    _cooldownService.SetCooldown(operation, msg);   // только при успехе
    await _operationCallRepository.RecordAsync(...);
}
catch (...) { ... }
```
Если операция упала (timeout xAI, ImageMagick OOM, network), кулдаун не выставляется → пользователь может спамить дорогой запрос. `SetCooldown` нужно делать **до** `OnMessageAsync` (либо в `finally`).

### ✅ 3.3 `TelegramHostedService` — fire-and-forget без отслеживания
`Saturn.Telegram.Lib/TelegramHostedService.cs:27-37`:
```csharp
_telegramBotClient.OnMessage += (msg, type) =>
{
    _ = Task.Run(() => _operationManager.MessageHandler(msg, type), cancellationToken);
    return Task.CompletedTask;
};
```
Проблемы:
- `cancellationToken` — это токен `StartAsync`, не `StopAsync`; при остановке хэндлеры не прерываются.
- Брошенный `Task` несвязан; исключение вне внутреннего try/catch потеряется (UnobservedTaskException).
- `StopAsync` возвращает `Task.CompletedTask` мгновенно — graceful shutdown отсутствует.

### ✅ 3.4 `CacheInvalidationService` — async-void в event handler, без reconnect
`Saturn.Telegram.Bot/Services/CacheInvalidationService.cs:22-61`:
```csharp
conn.Notification += async (_, args) => { ... };   // async void
```
- Исключение внутри = unhandled → потенциальный краш процесса.
- Если LISTEN-соединение к Postgres оборвётся, `WaitAsync` бросит → `BackgroundService` тихо умрёт без перезапуска. Инвалидация кэша перестанет работать без сигнала.

Решение: try/catch внутри handler + внешний retry-цикл с переоткрытием соединения и backoff.

### ✅ 3.5 Race condition в `SaveMessageService.ProcessUser/ProcessChat`
`Saturn.Telegram.Lib/Infrastructure/SaveMessageService.cs:74-86`. Два параллельных сообщения от одного нового user'а:
1. оба пройдут `GetCachedEntityById` → null,
2. оба вызовут `db.Users.AddAsync`,
3. один из `SaveChangesAsync` упадёт с PK violation → лог ошибки, сообщение не сохранится.

Решение: `INSERT ... ON CONFLICT DO NOTHING` через `ExecuteSqlInterpolatedAsync`, либо `try/catch DbUpdateException` с ретраем.

### ✅ 3.6 Двойная регистрация в DI
- `TelegramBotClient` регистрируется дважды: `Saturn.Telegram.Lib/Extensions/ServiceCollectionsExtensions.cs:27` и `Saturn.Telegram.Bot/Extensions/ServiceCollectionsExtensions.cs:28-32`. Побеждает последний, поведение совпадает, но это лишний путь конфигурации.
- `serviceCollection.Configure<BotOptions>(options => { })` — второй пустой блок (`ServiceCollectionsExtensions.cs:85-88`).
- `BotOptions.BotToken` заполняется, но **нигде не читается** — везде берут `configuration["BOT_TOKEN"]`. Лишний поверхностный путь для утечки токена в логи Options.

### ✅ 3.7 `IOperation` зарегистрирован как Singleton с зависимостями от scoped-логики
`Saturn.Telegram.Lib/Extensions/ServiceCollectionsExtensions.cs:21` — все операции Singleton. Сам по себе паттерн рабочий, но:
- зависимость от `IDbContextFactory` — корректна;
- `AddDbContextFactory<SaturnContext>(..., ServiceLifetime.Transient)` — параметр `lifetime` управляет лайфтаймом DbContext'а, выдаваемого фабрикой; сама фабрика всегда Singleton. Имя параметра путает; для ясности оставить дефолт (`Scoped`) и явно создавать через `CreateDbContextAsync()`.

### ✅ 3.8 Внедрение конкретных типов вместо интерфейсов
`TelegramBotClient` (вместо `ITelegramBotClient`), `OperationManager`, `CooldownService` инжектятся как concrete-классы в операциях и контроллерах. Это блокирует unit-тесты, моки, замену реализации.

### 🟡 3.9 `ShowFavStickOperation` — выборка всех стикеров в память
`Saturn.Telegram.Bot/Operations/Statistics/ShowFavStickOperation.cs:30-39` — `.ToListAsync()` затем `.GroupBy().OrderByDescending().FirstOrDefault()` в памяти. На активных пользователях — десятки тысяч строк. Переписать на SQL-агрегацию (`GroupBy` в `IQueryable`).

### 🟡 3.10 `ImagePromptRepository.ToDictionary` без дедупликации
`Saturn.Telegram.Db/Repositories/ImagePromptRepository.cs:30`:
```csharp
.SelectMany(...).ToDictionary(x => x.kw, x => x.Prompt);
```
Дублирующийся keyword (в двух промптах или в одном дважды) → `ArgumentException`. Использовать `.GroupBy(x => x.kw).ToDictionary(g => g.Key, g => g.First().Prompt)`, либо валидировать на insert в `ImagePromptsController`.

### 🟡 3.11 Дублирующая логика форматирования имени пользователя
Четыре варианта: `ChatGenerationOperation.GetSenderName` (две перегрузки), `NamorevoGoreController.FormatUserName`, `ChangeKarmaOperation.FormatUser`. Вынести в общий extension на `UserEntity`/`User`.

### 🟡 3.12 `Extension.GetSectionOrThrow` бросает `System.Exception`
`Saturn.Telegram.Bot/Extensions/Extension.cs:13,25` — `throw new Exception(...)` антипаттерн (ловится только catch-all). Заменить на `InvalidOperationException` / `OptionsValidationException`. Аналогично в `AddSaturnContext`.

### 🟡 3.13 `pg_notify` вызывается напрямую из контроллеров
`AiAgentsController`, `ChatsController`, `ImagePromptsController` — знание о механике инвалидации (имена каналов, формат) уехало в HTTP-слой. Вынести в `ICacheInvalidator` (Db-layer или отдельный сервис), имена каналов — в константы общего ассембли.

### 🟡 3.14 `NamorevoGoreController` — мёртвый блок и битый URL при отсутствии BOT_USERNAME
```csharp
if (!string.IsNullOrEmpty(_botUsername))
{
                              // пусто
}
var keyboard = ... $"https://t.me/{_botUsername}/namorevogore?startapp={request.ChatId}";
```
При `BOT_USERNAME=null` — `https://t.me//namorevogore?...` (битый). Валидировать `BOT_USERNAME` на старте либо обернуть формирование клавиатуры условием.

### 🟡 3.15 `AnimateOperation` — polling без CT + плохой паттерн
`Saturn.Telegram.Bot/Operations/Ai/AnimateOperation.cs:71-75`:
```csharp
while (!generateTask.IsCompleted)
{
    await _telegramBotClient.SendChatAction(...);
    await Task.Delay(TimeSpan.FromSeconds(4));   // не использует cts.Token
}
```
- При timeout `cts` отменяет `generateTask`, но `Task.Delay` тикает до 4 с.
- Сам paтерн «while polling» хуже, чем `Task.WhenAny(generateTask, Task.Delay(..., ct))`.

### 🟡 3.16 `IOperation` не получает `CancellationToken`
```csharp
public interface IOperation
{
    bool Validate(Message msg, UpdateType type);
    Task OnMessageAsync(Message msg, UpdateType type);   // нет CT
    Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
```
- Операции не могут корректно прерываться при shutdown.
- Документация (CLAUDE.md / AGENTS.md) обещает `(Message, CancellationToken)`, но реально это `(Message, UpdateType)`.

Рефакторинг ломающий, но необходимый: добавить `CancellationToken` параметром, прокинуть из `OperationManager`, использовать в HTTP-/EF-/Process-вызовах.

### 🟡 3.17 `DistortionService` — `Process` без timeout, без drain stdout/stderr
`Saturn.Telegram.Bot/Services/DistortionService.cs:56-65, 113-118, 124-133`:
```csharp
process.Start();
await process.WaitForExitAsync();   // без CT, без timeout
```
- Зависший ffmpeg блокирует семафор → весь сервис искажения встаёт.
- stdout/stderr не redirected — при заполнении буфера процесс может зависнуть.

Решение: timeout через CT, kill в catch, `RedirectStandardOutput/Error` + асинхронное чтение (как в `YtDlpSetupService.RunSelfUpdateAsync`).

### 🔵 3.18 `EscapeMarkdownV2` неполный/чувствительный к порядку
`Saturn.Telegram.Bot/Extensions/Extension.cs:34-50` — серия `Replace`. Если в тексте уже есть `\_`, второй проход не сломает, но в целом проще через один `Regex.Replace` или `Telegram.Bot.Helpers.Markdown.Escape`.

### 🔵 3.19 CORS-origin'ы захардкожены
`Saturn.Telegram.Api/Program.cs:18-22` — `https://routefabric.ru`, `https://midianok.github.io`. Вынести в конфигурацию.

### ⚪ 3.20 Магические строки имён pg_notify каналов
`agent_invalidation`, `chat_invalidation`, `image_prompt_invalidation` дублируются в API (источник) и в Bot (подписчик). Вынести в общую константу.

### 🟡 3.21 Декларация feature-флагов без реализации
В `.env`: `ImageDistortionOperationEnabled=true`, `SaveMessageOperationEnabled=true` и др. По коду эти ключи **никем не читаются**. Либо реализовать (например, проверять в `OperationManager.MessageHandler` `IsEnabled(operation)`), либо удалить из `.env`.

---

## 4. DevOps / Docker / CI

| | |
|---|---|
| 🟠 | Нет `restart: always` у `saturn` и `saturn-api` (есть только у `db`). При падении контейнер не поднимется. |
| 🟠 | Нет healthcheck'ов у `saturn`/`saturn-api`. Минимум — добавить `/health` endpoint в API и проверку polling-таска в боте. |
| 🟠 | `FfmpegSetupService` и `YtDlpSetupService` загружают бинарники из интернета **при каждом старте контейнера** (если volume не привязан). Риск: rate-limit GitHub Releases, лишний трафик, недетерминированный старт. Решение: смонтировать persistent volume на `/app/Tools`, либо встроить бинарники в Docker image на этапе сборки. |
| 🟡 | `Saturn.Telegram.Api/Dockerfile` `EXPOSE 8080 8081`, но `Program.cs:60` слушает `0.0.0.0:5001`. Несоответствие — порты в Dockerfile не используются. |
| 🟡 | CI собирает и сразу деплоит без тестов и без шага `dotnet list package --vulnerable`. Тестов в проекте нет вовсе (см. §5). |
| 🟡 | `docker compose pull/up` без проверки результата миграции. Если миграция упадёт, контейнер крашится в restart-loop, но CI отрапортует success. Добавить `docker compose ps`/healthcheck-ожидание после деплоя. |
| 🟡 | `ASPNETCORE_ENVIRONMENT` не задан в compose. См. §1.3. |
| 🔵 | `docker-compose.yaml`: завершающие пробелы, лишние пустые строки. Косметика. |

---

## 5. Покрытие тестами

🔴 **Тестов нет** (нет `*.Tests.csproj`, нет xUnit/NUnit). Для прод-релиза с нетривиальной логикой это критично. Минимум — unit-тесты:

| Цель | Что проверять |
|---|---|
| `TelegramInitDataMiddleware.Validate` | корректный HMAC, expired `auth_date`, malformed payload, отсутствие `hash`, regression на §1.2 |
| `CooldownService` | per-user, global-per-hour, admin bypass, корректность cache-key (включая мульти-чат) |
| `ChangeKarmaOperation.GetDelta`/`Normalize` | `+`, `++`, `спасибо`, `-`, `фу`, `null`, пустая строка, mixed-case |
| `Extension.EscapeMarkdownV2` | все спецсимволы, идемпотентность |
| `VideoUrlRegex` | TikTok, Instagram reel, не-видео ссылки, ложные срабатывания |

---

## 6. План исправлений (приоритизированный)

### Обязательно к релизу (P0)
1. ✅ §1.2 — проверка `auth_date` + `FixedTimeEquals` в `TelegramInitDataMiddleware`.
2. ✅ §1.3 — зафиксировать `ASPNETCORE_ENVIRONMENT=Production` в compose.
3. ✅ §1.1 — `git rm --cached .env`, добавить secret-scanning hook.
4. ✅ §3.1 — починить sync-over-async в `ChatGenerationOperation.IsReplyToBot`.
5. 🚫 §3.2 — `SetCooldown` ставить даже при исключении операции (в `finally`). [игнорируется]
6. 🟡 §1.6 — убрать `<NoWarn>NU1901-NU1904</NoWarn>`, прогнать `dotnet list package --vulnerable`.
7. 🔴 §2.3 — добавить `INVOKE_COMMAND` в `.env.example`.

### Желательно к релизу (P1)
8. ✅ §1.4 — авторизация по `chat_id` в контроллерах API.
9. 🟠 §1.5 — SSH-деплой по ключу.
10. §2.1–2.4 — синхронизировать README/CLAUDE.md/AGENTS.md с реальностью (xAI вместо OpenAI, GHCR вместо Docker Hub, актуальные версии пакетов, реальные команды, убрать `IMAGE_MANIPULATION_SERVICE_URL`).
11. ✅ §3.4 — try/catch + reconnect в `CacheInvalidationService`.
12. 🟠 §3.5 — `ON CONFLICT DO NOTHING` в `SaveMessageService`.
13. 🟠 §4 — `restart: always` + healthcheck'и для bot/api; убрать `15432:5432` наружу (§1.7); закэшировать ffmpeg/yt-dlp в volume или image.
14. 🟡 §4 — выровнять API EXPOSE и `Run("...:5001")`.
15. 🟡 §3.14 — починить `NamorevoGoreController` (мёртвый if + битый URL).
16. 🟡 §5 — добавить тесты на critical path (middleware, cooldown, karma, EscapeMarkdownV2, VideoUrlRegex).
17. 🟡 §4 — CI: `dotnet test`, `dotnet list package --vulnerable`, проверка форматирования.

### Технический долг (P2)
18. 🟡 §3.16 — добавить `CancellationToken` в `IOperation.OnMessageAsync`, прокинуть из менеджера.
19. ✅ §3.8 — заменить инжекцию `TelegramBotClient` на `ITelegramBotClient`; ввести интерфейсы для `OperationManager`/`CooldownService` где это даёт пользу.
20. 🟡 §3.13 — вынести `pg_notify` в `ICacheInvalidator`, имена каналов — в константы.
21. ✅ §3.6 — убрать двойную регистрацию `TelegramBotClient` и пустой `Configure<BotOptions>`; удалить неиспользуемый `BotOptions.BotToken`.
22. 🟡 §3.9 — `ShowFavStickOperation` на SQL-агрегацию.
23. 🟡 §3.10 — дедуп ключевых слов в `ImagePromptRepository`.
24. 🟡 §3.17 — timeouts + std-out drain для всех `Process` в `DistortionService`.
25. 🟡 §3.15 — `AnimateOperation` через `Task.WhenAny`, `Task.Delay` с CT.
26. 🟡 §3.12 — заменить `throw new Exception` на типизированные.
27. 🟡 §3.21 — реализовать feature-флаги или удалить из конфигурации.
28. 🟡 §3.11 — единая утилита форматирования имени пользователя.

### Косметика (P3)
29. 🔵 §3.18 — `EscapeMarkdownV2` через helper.
30. 🔵 §3.19 — CORS-origin'ы из конфигурации.
31. ⚪ §3.20 — константы для имён pg_notify каналов.
32. ⚪ удалить пустой `Saturn.Telegram.Bot/Services/Scheduled/`.

---

## Резюме

Блокеры релиза:
- §1.2 (auth_date) — токен-replay в API;
- §1.3 (auth только в Production) — риск открытой API при ошибке окружения;
- §3.1 (sync-over-async) — потенциальный starvation thread pool;
- ~~§3.2 (cooldown не ставится при ошибке)~~ — игнорируется;
- §2.3 (`.env.example` без `INVOKE_COMMAND`) — чистая среда не запустится.

Без перечисленных P0 катить нельзя. P1 — закрыть в течение первого спринта после релиза; P2/P3 — плановый рефакторинг.
