using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProdFalcon.API.Versioning;

namespace ProdFalcon.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api")]
public class MetaController : ControllerBase
{
    [HttpGet("version")]
    [DisableRateLimiting]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            name = "ProdFalcon API",
            version = ApiVersionInfo.ProductVersion,
            apiVersion = ApiVersionInfo.Current
        });
    }
}
