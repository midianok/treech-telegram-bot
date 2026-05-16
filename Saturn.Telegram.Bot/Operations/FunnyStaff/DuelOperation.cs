using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

[ChatOnly]
[Cooldown(300, "подожди немного перед следующей дуэлью")]
public class DuelOperation : IOperation
{
    private const int StartHp = 5;
    private const int MaxRounds = 6;
    private const int WinPoints = 10;
    private const int LosePoints = 5;

    private static readonly ConcurrentDictionary<long, bool> ActiveDuels = new();

    private static readonly string[] AttackPhrases =
    [
        "наносит удар", "атакует", "делает выпад", "бьёт", "атакует молниеносно"
    ];

    private static readonly string[] MissPhrases =
    [
        "промахивается", "бьёт мимо", "теряет равновесие", "спотыкается"
    ];

    private static readonly string[] BlockPhrases =
    [
        "блокирует удар", "уклоняется", "парирует", "уходит в сторону"
    ];

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly Random _random = new();

    public DuelOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (msg.Text == null) return false;
        var text = msg.Text.Trim();
        if (text.Equals("дуэль", StringComparison.OrdinalIgnoreCase))
            return msg.ReplyToMessage?.From != null && !msg.ReplyToMessage.From.IsBot;
        return text.StartsWith("дуэль @", StringComparison.OrdinalIgnoreCase);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.From == null) return;

        var challenger = msg.From;
        var chatId = msg.Chat.Id;

        if (ActiveDuels.ContainsKey(chatId))
        {
            await _telegramBotClient.SendMessage(chatId, "в чате уже идёт дуэль!",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        User? opponent = null;

        if (msg.ReplyToMessage?.From != null && !msg.ReplyToMessage.From.IsBot)
        {
            opponent = msg.ReplyToMessage.From;
        }
        else
        {
            var username = ParseUsername(msg.Text!);
            if (username != null)
            {
                var userEntity = await db.Users
                    .FirstOrDefaultAsync(x => x.Username != null &&
                        x.Username.ToLower() == username.ToLower());
                if (userEntity != null)
                {
                    opponent = new User
                    {
                        Id = userEntity.Id,
                        FirstName = userEntity.FirstName ?? "",
                        LastName = userEntity.LastName,
                        Username = userEntity.Username,
                    };
                }
            }
        }

        if (opponent == null)
        {
            await _telegramBotClient.SendMessage(chatId, "не могу найти этого пользователя",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        if (opponent.Id == challenger.Id)
        {
            await _telegramBotClient.SendMessage(chatId, "нельзя вызвать себя на дуэль",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        if (!ActiveDuels.TryAdd(chatId, true)) return;

        try
        {
            await RunDuelAsync(db, msg, challenger, opponent);
        }
        finally
        {
            ActiveDuels.TryRemove(chatId, out _);
        }
    }

    private async Task RunDuelAsync(SaturnContext db, Message msg, User challenger, User opponent)
    {
        var challengerName = FormatName(challenger);
        var opponentName = FormatName(opponent);

        var battle = SimulateBattle();

        var sent = await _telegramBotClient.SendMessage(
            msg.Chat.Id,
            BuildFrame(challengerName, opponentName, StartHp, StartHp, "⚔️ дуэль начинается..."),
            ParseMode.None);

        await Task.Delay(1200);

        int challengerHp = StartHp;
        int opponentHp = StartHp;

        foreach (var round in battle.Rounds)
        {
            challengerHp = Math.Max(0, challengerHp - round.DamageToChallenger);
            opponentHp = Math.Max(0, opponentHp - round.DamageToOpponent);

            var roundText = BuildRoundText(challengerName, opponentName, round);
            await _telegramBotClient.EditMessageText(
                msg.Chat.Id,
                sent.Id,
                BuildFrame(challengerName, opponentName, challengerHp, opponentHp, roundText));

            await Task.Delay(_random.Next(800, 1400));

            if (challengerHp == 0 || opponentHp == 0) break;
        }

        var (winnerId, loserId, winnerName, loserName) = DetermineWinner(
            challenger, opponent, challengerHp, opponentHp, challengerName, opponentName);

        var (winnerPoints, loserPoints) = await UpdatePointsAsync(db, msg.Chat.Id, winnerId, loserId);

        var finalText = challengerHp == opponentHp
            ? BuildDrawFrame(challengerName, opponentName, winnerPoints, loserPoints)
            : BuildFinalFrame(challengerName, opponentName, challengerHp, opponentHp, winnerName, loserName, winnerPoints, loserPoints);

        await _telegramBotClient.EditMessageText(msg.Chat.Id, sent.Id, finalText);
    }

    private string BuildFrame(string a, string b, int hpA, int hpB, string status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚔️  Д У Э Л Ь  ⚔️");
        sb.AppendLine();
        sb.AppendLine($"🗡️ {a}");
        sb.AppendLine(HpBar(hpA));
        sb.AppendLine();
        sb.AppendLine($"🛡️ {b}");
        sb.AppendLine(HpBar(hpB));
        sb.AppendLine();
        sb.Append(status);
        return sb.ToString();
    }

    private static string HpBar(int hp)
    {
        var filled = string.Concat(Enumerable.Repeat("❤️", hp));
        var empty = string.Concat(Enumerable.Repeat("🖤", StartHp - hp));
        return filled + empty;
    }

    private string BuildRoundText(string a, string b, RoundResult round)
    {
        var lines = new List<string>();

        if (round.DamageToOpponent > 0)
            lines.Add($"💥 {a} {Pick(AttackPhrases)}! (-{round.DamageToOpponent}❤️)");
        else if (round.ChallengerMissed)
            lines.Add($"💨 {a} {Pick(MissPhrases)}");
        else
            lines.Add($"🛡️ {b} {Pick(BlockPhrases)}");

        if (round.DamageToChallenger > 0)
            lines.Add($"💥 {b} {Pick(AttackPhrases)}! (-{round.DamageToChallenger}❤️)");
        else if (round.OpponentMissed)
            lines.Add($"💨 {b} {Pick(MissPhrases)}");
        else
            lines.Add($"🛡️ {a} {Pick(BlockPhrases)}");

        return string.Join("\n", lines);
    }

    private static string BuildFinalFrame(string a, string b, int hpA, int hpB,
        string winner, string loser, int winnerPts, int loserPts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚔️  Д У Э Л Ь  ⚔️");
        sb.AppendLine();
        sb.AppendLine($"🗡️ {a}");
        sb.AppendLine(HpBarStatic(hpA));
        sb.AppendLine();
        sb.AppendLine($"🛡️ {b}");
        sb.AppendLine(HpBarStatic(hpB));
        sb.AppendLine();
        sb.AppendLine($"🏆 {winner} победил!");
        sb.AppendLine($"💀 {loser} пал в поединке");
        sb.AppendLine();
        sb.AppendLine($"📊 {winner}: +{WinPoints} очков (всего: {winnerPts})");
        sb.Append($"📊 {loser}: -{LosePoints} очков (всего: {loserPts})");
        return sb.ToString();
    }

    private static string BuildDrawFrame(string a, string b, int ptsA, int ptsB)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚔️  Д У Э Л Ь  ⚔️");
        sb.AppendLine();
        sb.AppendLine($"🗡️ {a}");
        sb.AppendLine(HpBarStatic(StartHp / 2));
        sb.AppendLine();
        sb.AppendLine($"🛡️ {b}");
        sb.AppendLine(HpBarStatic(StartHp / 2));
        sb.AppendLine();
        sb.AppendLine("🤝 Ничья! Дуэлянты разошлись с миром");
        sb.AppendLine();
        sb.AppendLine($"📊 {a}: {ptsA} очков");
        sb.Append($"📊 {b}: {ptsB} очков");
        return sb.ToString();
    }

    private static string HpBarStatic(int hp)
    {
        var filled = string.Concat(Enumerable.Repeat("❤️", Math.Max(0, hp)));
        var empty = string.Concat(Enumerable.Repeat("🖤", Math.Max(0, StartHp - hp)));
        return filled + empty;
    }

    private BattleResult SimulateBattle()
    {
        var rounds = new List<RoundResult>();
        int hpA = StartHp, hpB = StartHp;

        for (int i = 0; i < MaxRounds && hpA > 0 && hpB > 0; i++)
        {
            var round = SimulateRound();
            hpA = Math.Max(0, hpA - round.DamageToChallenger);
            hpB = Math.Max(0, hpB - round.DamageToOpponent);
            rounds.Add(round);
        }

        return new BattleResult(rounds);
    }

    private RoundResult SimulateRound()
    {
        var (dmgToB, aBlocked) = RollAttack();  // challenger attacks opponent
        var (dmgToA, bBlocked) = RollAttack();  // opponent attacks challenger
        return new RoundResult(
            DamageToChallenger: dmgToA,
            DamageToOpponent: dmgToB,
            ChallengerMissed: dmgToB == 0 && !aBlocked,
            OpponentMissed: dmgToA == 0 && !bBlocked);
    }

    private (int damage, bool blocked) RollAttack()
    {
        var roll = _random.Next(0, 10);
        if (roll < 3) return (0, false);     // miss
        if (roll < 5) return (0, true);      // blocked
        return (_random.Next(1, 3), false);  // hit 1-2
    }

    private static (long winnerId, long loserId, string winnerName, string loserName) DetermineWinner(
        User challenger, User opponent,
        int hpA, int hpB, string nameA, string nameB)
    {
        if (hpA >= hpB)
            return (challenger.Id, opponent.Id, nameA, nameB);
        return (opponent.Id, challenger.Id, nameB, nameA);
    }

    private async Task<(int winnerPts, int loserPts)> UpdatePointsAsync(
        SaturnContext db, long chatId, long winnerId, long loserId)
    {
        var winner = await GetOrCreatePoints(db, winnerId, chatId);
        var loser = await GetOrCreatePoints(db, loserId, chatId);

        winner.Points += WinPoints;
        loser.Points = Math.Max(0, loser.Points - LosePoints);

        await db.SaveChangesAsync();
        return (winner.Points, loser.Points);
    }

    private static async Task<GamePointsEntity> GetOrCreatePoints(SaturnContext db, long userId, long chatId)
    {
        var entity = await db.GamePoints.AsTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChatId == chatId);

        if (entity != null) return entity;

        entity = new GamePointsEntity { UserId = userId, ChatId = chatId, Points = 0 };
        await db.GamePoints.AddAsync(entity);
        return entity;
    }

    private static string? ParseUsername(string text)
    {
        var lower = text.Trim();
        if (!lower.StartsWith("дуэль @", StringComparison.OrdinalIgnoreCase)) return null;
        var username = text.Trim()["дуэль @".Length..].Trim();
        return string.IsNullOrEmpty(username) ? null : username;
    }

    private static string FormatName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username)) return $"@{user.Username}";
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? user.Id.ToString() : name;
    }

    private string Pick(string[] options) => options[_random.Next(options.Length)];

    private record BattleResult(List<RoundResult> Rounds);

    private record RoundResult(int DamageToChallenger, int DamageToOpponent, bool ChallengerMissed, bool OpponentMissed);
}
