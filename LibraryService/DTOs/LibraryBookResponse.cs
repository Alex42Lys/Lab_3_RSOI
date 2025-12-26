namespace LibraryService.DTOs
{
    public class LibraryBookResponse
    {
        public Guid BookUid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Condition { get; set; } 
        public int AvailableCount { get; set; }
    }
}
