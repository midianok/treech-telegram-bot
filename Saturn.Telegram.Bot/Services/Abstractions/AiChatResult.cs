namespace Saturn.Bot.Service.Services.Abstractions;

/// <summary>Answer of the model after a tool-calling loop.</summary>
/// <param name="Text">Final text answer.</param>
/// <param name="ToolCalls">Distinct names of the tools the model called, in call order.</param>
public record AiChatResult(string Text, IReadOnlyList<string> ToolCalls);
