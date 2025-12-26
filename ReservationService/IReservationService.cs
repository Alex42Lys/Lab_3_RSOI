using ReservationService.Models;

namespace ReservationService
{
    public interface IReservationService
    {
        public Task<List<Reservation>> GetAllRentedUserReservations(string userName);
        public Task<Reservation> CreateNewReservation(string userName, Guid bookId, Guid libId, DateTime tillDate);
        public Task<Reservation> CloseReservation(string userName, Guid reservationId, DateTime returnDate);

    }
}
