namespace Saturn.Telegram.Api.Dto;

public record OperationCallDto(string OperationName, DateTime CalledAt, long UserId, string UserName);