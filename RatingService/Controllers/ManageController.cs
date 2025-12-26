using Microsoft.AspNetCore.Mvc;

namespace RatingService.Controllers
{
    [ApiController]
    [Route("manage")]
    public class ManageController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok();
        }
    }
}