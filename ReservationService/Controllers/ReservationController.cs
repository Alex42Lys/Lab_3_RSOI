using Microsoft.AspNetCore.Mvc;
using ReservationService.DTOs;
using ReservationService.Models;

namespace ReservationService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReservationController : ControllerBase
    {
        private readonly IReservationService _reservationService;   
        public ReservationController (IReservationService reservationService)
        {
            _reservationService = reservationService;
        }
        [HttpGet("AllReservations")]
        public async Task<ActionResult> GetAllRentedUserReservations()
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is required");
            }
            var res = await _reservationService.GetAllRentedUserReservations(username);
            return Ok(res);
        }
        [HttpPost("CreateNewReservation")]
        public async Task<ActionResult> CreateNewReservation([FromBody] TakeBookRequest request)
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is required");
            }

            var res = await _reservationService.CreateNewReservation(username,
               Guid.Parse(request.BookUid),
               Guid.Parse(request.LibraryUid),
               Convert.ToDateTime(request.TillDate));
            return Ok(res);
        }

        [HttpPost("CloseReservation")]
        public async Task<ActionResult> CloseReservation(string resId, [FromBody] ReturnBookRequest request)
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is required");
            }

            var res = await _reservationService.CloseReservation(username, Guid.Parse(resId), Convert.ToDateTime(request.Date));
            return Ok(res);
        }
    }
}
