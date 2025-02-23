using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Operations.Abstractions;

namespace Saturn.Bot.Service.Operations;

public class SendOperations : OperationBase
{
    public SendOperations(ILogger<IOperation> logger, IConfiguration configuration) : base(logger, configuration)
    {
        
    }
}