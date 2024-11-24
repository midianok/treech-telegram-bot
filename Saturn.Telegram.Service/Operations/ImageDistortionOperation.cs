using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Operations.Abstractions;

namespace Saturn.Bot.Service.Operations;

public class ImageDistortionOperation : OperationBase
{
    public ImageDistortionOperation(ILogger<IOperation> logger, IConfiguration configuration) : base(logger, configuration)
    {
    }
}