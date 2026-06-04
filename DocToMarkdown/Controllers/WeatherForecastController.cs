using Microsoft.AspNetCore.Mvc;

namespace DocToMarkdown.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("API is running...");
        }
    }
}