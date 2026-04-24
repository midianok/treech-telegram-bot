namespace Saturn.Telegram.Lib.Exceptions;

public class AiBudgetExhaustedException : Exception
{
    public AiBudgetExhaustedException() : base("xAI balance exhausted") { }
}
