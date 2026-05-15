using System.Text.Json;

namespace Saturn.Telegram.Api.Dto;

public record CreateLogRequest(
    string Source,
    string Level,
    string Message,
    JsonElement Data
);
