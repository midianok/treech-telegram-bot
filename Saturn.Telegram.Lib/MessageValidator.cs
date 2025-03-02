using FluentValidation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

public class MessageValidator : AbstractValidator<(Message Message, UpdateType UpdateType)>;