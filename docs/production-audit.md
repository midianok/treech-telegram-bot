# Аудит: подготовка к продакшену

Дата: 2026-05-22

## Захардкоженные значения для трич-чата

### ~~1. Имя бота `TreechBot` — 5 мест~~ ✅

Заменено на `BotOptions.BotUsername` (env: `BOT_USERNAME`).

### ~~2. Юзернейм `"Olegex3"` — `OlegexOperation.cs:10`~~ ✅

Перенесён в `AdminOptions.EasterEggUsername` (env: `EASTER_EGG_USERNAME`). Если переменная не задана, операция отключена.

### ~~3. Юзернейм `"ilya_naprimer"` — `CooldownService.cs:27,60`~~ ✅

Перенесён в `AdminOptions.AdminUsername` (env: `ADMIN_USERNAME`).

### ~~4. User ID `198607451` — `AnimateOperation.cs:13`~~ ✅

Атрибут `[Allow(198607451)]` убран. Доступ проверяется по `AdminOptions.AdminUsername`.

### ~~5. Bot User ID `5990847351` — 4 места~~ ✅

Заменено флагом `IsBot` в таблице `messages` (миграция `20260522000000_AddIsBotToMessages`). Флаг выставляется при сохранении сообщения через `msg.From.IsBot`.

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

#### ~~3. `ChatCachedRepository.GetAsync` — `SingleAsync` упадёт для нового чата~~ ✅

`SingleAsync` заменён на `SingleOrDefaultAsync` с fallback `new ChatEntity { Id = chatId }`.
Если чат ещё не сохранён в БД — возвращается минимальная сущность без агента, операция продолжается без краша.

### 🟡 Значимо

#### ~~4. `WhoTodayOperation` — null Username → `"@ сегодня ..."`~~ ✅

Запрос теперь выбирает `{ Username, FirstName }`. Отображаемое имя: `@username` если есть, иначе `FirstName`.

#### 5. `GuessWhoOperation.ActiveGames` — статический словарь теряется при рестарте

`static ConcurrentDictionary<long, GuessWhoGame>` — при перезапуске бота все активные игры
пропадают, а кнопки в чате остаются. Пользователь нажмёт — получит "Игра уже закончилась".

### 🟠 Безопасность

#### ~~6. `.env` не добавлен в `.gitignore`~~ ✅

`.env` добавлен в `.gitignore`.

#### ~~7. `launchSettings.json` содержит реальные API-ключи~~ ✅ частично

Оба `launchSettings.json` добавлены в `.gitignore` и не коммитятся. Реальные ключи на диске остаются — для локальной разработки допустимо.

---

## Сводка задач

| Приоритет | Задача | Статус |
|-----------|--------|--------|
| 🔴 | Добавить `BOT_USERNAME` в env, заменить 5 хардкодов `TreechBot` | ✅ |
| 🔴 | `ChatCachedRepository.GetAsync`: `SingleAsync` → `SingleOrDefaultAsync` с fallback | ✅ |
| 🔴 | Решить, нужна ли `ChatId` в `NamorevoGoreScoreEntity` и `UserKarmaEntity` | ❌ |
| 🟡 | `WhoTodayOperation`: null username fallback на `FirstName` | ✅ |
| 🟡 | Перенести `"ilya_naprimer"` / `198607451` в конфиг | ✅ |
| 🟡 | Получать bot user ID вместо 4 хардкодов `5990847351` | ✅ (флаг `IsBot`) |
| 🟠 | Добавить `.env` в `.gitignore` | ✅ |
| 🟠 | Убрать реальные ключи из `launchSettings.json` | ✅ (файл в `.gitignore`) |
