namespace LibraryService.DTOs
{
    public class LibraryPaginationResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalElements { get; set; }
        public List<LibraryResponse> Items { get; set; } = new();
    }
}
