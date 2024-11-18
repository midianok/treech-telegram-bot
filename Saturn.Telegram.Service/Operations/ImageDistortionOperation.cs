using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Operations.Abstractions;

namespace Saturn.Bot.Service.Operations;

public class ImageDistortionOperation : OperationBase
{
    public ImageDistortionOperation(ILogger<ImageDistortionOperation> logger) : base(logger)
    {
    }
}