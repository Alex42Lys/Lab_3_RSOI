namespace LibraryService.DTOs
{
    public class LibraryResponse
    {
        public Guid LibraryUid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}
