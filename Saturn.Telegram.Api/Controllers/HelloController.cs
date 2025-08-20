using Microsoft.AspNetCore.Mvc;

namespace Saturn.Telegram.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Привет из .NET 9 Web API!");
}