using ReservationService.Models;

namespace ReservationService
{
    public class ReservationService : IReservationService
    {
        private readonly IReservationRepository _reservationRepository;
        public ReservationService(IReservationRepository reservationRepository ) 
        {
            _reservationRepository = reservationRepository;
        }

        public async Task<List<Reservation>> GetAllRentedUserReservations(string userName)
        {
            var res = await _reservationRepository.GetAllRentedUserReservations(userName);



            return res;
        }

        public async Task<Reservation> CreateNewReservation(string userName, Guid bookId, Guid libId, DateTime tillDate)
        {
            var startDate = DateTime.Now;
            var res = await _reservationRepository.CreateNewReservation(userName, bookId, libId, startDate, tillDate);
            return res;
        }

        public async Task<Reservation> CloseReservation(string userName, Guid reservationId, DateTime returnDate)
        {
            var res = await _reservationRepository.CloseReservation(userName, reservationId, returnDate);
            return res;
        }
    }
}
