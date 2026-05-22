# Аудит: подготовка к продакшену

Дата: 2026-05-22

## Захардкоженные значения для трич-чата

### 1. Имя бота `TreechBot` — 5 мест

| Файл | Строка |
|------|--------|
| `Saturn.Telegram.Bot/Operations/Infrastructure/BotOperation.cs` | 18 |
| `Saturn.Telegram.Bot/Operations/Statistics/ShowUserStatOperation.cs` | 48 |
| `Saturn.Telegram.Bot/Operations/Statistics/ShowTopStatOperation.cs` | 58 |
| `Saturn.Telegram.Bot/Operations/FunnyStaff/NamorevoGoreOperation.cs` | 42 |
| `Saturn.Telegram.Api/Controllers/NamorevoGoreController.cs` | 51 |

Нет конфигурационной переменной `BOT_USERNAME`. Нужно добавить её в env и заменить хардкоды, либо получать через `botClient.GetMe()`.

### 2. Юзернейм `"Olegex3"` — `OlegexOperation.cs:10`

Пасхалка для конкретного участника трич-чата. Полностью чат-специфична.

### 3. Юзернейм `"ilya_naprimer"` — `CooldownService.cs:27,60`

Хардкод для обхода кулдауна администратором. Должен быть в конфиге.

### 4. User ID `198607451` — `AnimateOperation.cs:13`

`[Allow(198607451)]` — доступ к команде только Илье. Закомментирован как `//ilya_naprimer`. Должен быть в конфиге.

### 5. Bot User ID `5990847351` — 4 места

ID самого бота используется для фильтрации его сообщений из выборок:

| Файл | Строка |
|------|--------|
| `Saturn.Telegram.Bot/Operations/Ai/SilenceOperation.cs` | 19 |
| `Saturn.Telegram.Bot/Operations/Statistics/ShowTopWordsOperation.cs` | 15 |
| `Saturn.Telegram.Bot/Operations/Ai/SummaryOperation.cs` | 59 |
| `Saturn.Telegram.Api/Controllers/StatsController.cs` | 44 |

Можно получать динамически через `botClient.GetMe()` — по аналогии с `ChatGenerationOperation.IsReplyToBot`.

---

## Потенциальные проблемы при работе в нескольких чатах

### 🔴 Критично

#### 1. `NamorevoGoreScoreEntity` — глобальная таблица, нет `ChatId`

Таблица `namorevo_gore_scores` имеет только `UserId` как PK — результаты игры едины для всех чатов.
`NamorevoGoreOperation` показывает глобальный топ без фильтрации по чату.
Если бот стоит в нескольких чатах — они делят один лидерборд.

#### 2. `UserKarmaEntity` — глобальная карма, нет `ChatId`

Карма хранится только по `UserId`. Изменение кармы в чате A влияет на карму в чате B.
Кулдаун на изменение тоже глобальный — `KarmaChangeEntity` проверяется по `fromUser.Id`
без фильтрации по чату (`ChangeKarmaOperation.cs:57–65`).

#### 3. `ChatCachedRepository.GetAsync` — `SingleAsync` упадёт для нового чата

```csharp
// Saturn.Telegram.Db/Repositories/ChatCachedRepository.cs:26
return await context.Chats.Include(x => x.AiAgent).SingleAsync(x => x.Id == chatId);
```

Если `SaveMessageAsync` упал молча (он глотает все исключения), а потом `ChatGenerationOperation`
вызывает `GetAsync` для незарегистрированного чата — `SingleAsync` бросит `InvalidOperationException`.
Нужно заменить на `SingleOrDefaultAsync` с fallback.

### 🟡 Значимо

#### 4. `WhoTodayOperation` — null Username → `"@ сегодня ..."`

```csharp
// Saturn.Telegram.Bot/Operations/FunnyStaff/WhoTodayOperation.cs:31-45
.Select(x => x.User!.Username) // может быть null у пользователей без юзернейма
...
await _telegramBotClient.SendMessage(msg.Chat, $"@{randomUser} сегодня {todayMessage}");
```

Если у случайного пользователя нет юзернейма — бот отправит `"@ сегодня ..."`.
Нужен fallback на `FirstName`.

#### 5. `GuessWhoOperation.ActiveGames` — статический словарь теряется при рестарте

`static ConcurrentDictionary<long, GuessWhoGame>` — при перезапуске бота все активные игры
пропадают, а кнопки в чате остаются. Пользователь нажмёт — получит "Игра уже закончилась".

### 🟠 Безопасность

#### 6. `.env` содержит реальные секреты в корне репозитория

Файл `.env` содержит токен бота, строку подключения к БД и ключи xAI.
Необходимо убедиться, что файл добавлен в `.gitignore` и не закоммичен.

#### 7. `launchSettings.json` содержит реальные API-ключи

`Saturn.Telegram.Bot/Properties/launchSettings.json` содержит ключи xAI и строку подключения
к продакшн БД (`152.53.94.213:15432`). Этот файл обычно коммитится — ключи нужно убрать.

---

## Сводка задач

| Приоритет | Задача |
|-----------|--------|
| 🔴 | Добавить `BOT_USERNAME` в env, заменить 5 хардкодов `TreechBot` |
| 🔴 | `ChatCachedRepository.GetAsync`: `SingleAsync` → `SingleOrDefaultAsync` с fallback |
| 🔴 | Решить, нужна ли `ChatId` в `NamorevoGoreScoreEntity` и `UserKarmaEntity` |
| 🟡 | `WhoTodayOperation`: null username fallback на `FirstName` |
| 🟡 | Перенести `"ilya_naprimer"` / `198607451` в конфиг (`ADMIN_USER_IDS`) |
| 🟡 | Получать bot user ID через `GetMe()` вместо 4 хардкодов `5990847351` |
| 🟠 | Убедиться что `.env` в `.gitignore` |
| 🟠 | Убрать реальные ключи из `launchSettings.json` |
