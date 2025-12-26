using Microsoft.AspNetCore.Mvc;

namespace ReservationService.Controllers
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