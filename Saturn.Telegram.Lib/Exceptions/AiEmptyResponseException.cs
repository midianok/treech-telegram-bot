namespace Saturn.Telegram.Lib.Exceptions;

public class AiEmptyResponseException : Exception
{
    public AiEmptyResponseException() : base("Empty response from AI") { }
}
