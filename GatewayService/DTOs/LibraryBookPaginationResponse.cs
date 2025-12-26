namespace GatewayService.DTOs
{
    public class LibraryBookPaginationResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalElements { get; set; }
        public List<LibraryBookResponse> Items { get; set; } = new();
    }
}
