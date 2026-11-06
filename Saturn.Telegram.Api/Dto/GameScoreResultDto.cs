namespace Saturn.Telegram.Api.Dto;

/// <summary>Итог отправки результата игры: лучший счёт игрока в чате после сохранения и побит ли рекорд.</summary>
public record GameScoreResultDto(int BestScore, bool IsNewRecord);
