using Microsoft.AspNetCore.Mvc;
using Saturn.Telegram.Api.Middleware;

namespace Saturn.Telegram.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected long? GetCurrentUserId()
    {
        return HttpContext.Items.TryGetValue(TelegramInitDataMiddleware.UserIdItemKey, out var v) && v is long id
            ? id
            : null;
    }
}
