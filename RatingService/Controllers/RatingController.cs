using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace RatingService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RatingController : ControllerBase
    {
        private readonly IRatingService _ratingService;


        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpGet("rating")]
        public async Task<ActionResult<UserRatingResponse>> GetUserRating()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return StatusCode(400, new ErrorResponse { Message = "No user" });
                }
                var ratingResponse = await _ratingService.GetUserRating(username);

                return Ok(ratingResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        [HttpPost("changeRating")]
        public async Task<ActionResult<UserRatingResponse>> ChangeUserRating(
            [FromQuery] int delta)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return StatusCode(400, new ErrorResponse { Message = "No user" });
                }
                var ratingResponse = await _ratingService.ChangeUserRating(username, delta);

                return Ok(ratingResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

    }
}
