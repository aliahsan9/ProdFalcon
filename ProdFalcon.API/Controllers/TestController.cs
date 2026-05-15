using Microsoft.AspNetCore.Mvc;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("ProdFalcon API Running");
    }
}