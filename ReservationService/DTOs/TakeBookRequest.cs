namespace ReservationService.DTOs
{
    public class TakeBookRequest
    {
        public string BookUid { get; set; }
        public string LibraryUid { get; set; }
        public string TillDate { get; set; }
    }
}
