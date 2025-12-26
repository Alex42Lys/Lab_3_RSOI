using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService
{
    public class ReservationRepository(PostgresContext _context): IReservationRepository
    {
        public async Task<List<Reservation>> GetAllUserReservations(string userName)
        {
            var reservations = await _context.Reservations.Where(x => x.Username == userName).ToListAsync();
            return reservations;
        }

        public async Task<List<Reservation>> GetAllRentedUserReservations(string userName)
        {
            var reservations = await _context.Reservations.Where(x => x.Username == userName && x.Status == "RENTED").ToListAsync();
            return reservations;
        }
        public async Task<Reservation> CreateNewReservation(string userName, Guid bookId, Guid libId, 
            DateTime startDate, DateTime tillDate)
        {
            var resId = Guid.NewGuid(); 
            await _context.Reservations.AddAsync(new Reservation()
            {
                ReservationUid = resId,
                Username = userName,
                BookUid = bookId,
                LibraryUid = libId,
                StartDate = startDate,
                TillDate = tillDate,
                Status = "RENTED"
            });
            await _context.SaveChangesAsync();
            var res = await _context.Reservations.Where(x => x.ReservationUid == resId).FirstOrDefaultAsync();
            return res;
        }
        public async Task<Reservation> CloseReservation(string userName, Guid reservationId, DateTime returnDate)
        {
            
            var reservation = await (_context.Reservations.Where(x => x.Username == userName && x.ReservationUid == reservationId)).FirstOrDefaultAsync();
            if (returnDate <= reservation.TillDate)
                reservation.Status = "RETURNED";
            else
                reservation.Status = "EXPIRED";
            await _context.SaveChangesAsync();
            var res = await (_context.Reservations.Where(x => x.Username == userName && x.ReservationUid == reservationId)).FirstOrDefaultAsync();
            return res;
        }
    }
}
