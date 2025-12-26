namespace ReservationService.DTOs
{
    public class TakeBookResponse
    {
        public string ReservationUid { get; set; }
        public string Status { get; set; }
        public string StartDate { get; set; }
        public string TillDate { get; set; }
        public BookInfo Book { get; set; }
        public LibraryResponse Library { get; set; }
        public UserRatingResponse Rating { get; set; }
    }
}
