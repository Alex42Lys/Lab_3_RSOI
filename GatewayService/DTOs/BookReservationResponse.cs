namespace GatewayService.DTOs
{
    public class BookReservationResponse
    {
        public string ReservationUid { get; set; }
        public string Status { get; set; }
        public string StartDate { get; set; }
        public string TillDate { get; set; }
        public BookInfo Book { get; set; }
        public LibraryResponse Library { get; set; }
    }
}
