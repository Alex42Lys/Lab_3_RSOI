using LibraryService.DTOs;
using LibraryService.Models;

namespace LibraryService
{
    public interface ILibraryService
    {
        public Task<List<LibraryResponse>> GetLibrariesByCity(string city);
        public Task<List<LibraryBookResponse>> GetBooksByLibrary(Guid libraryUid, bool showAll);
        public Task<int> ChangeCount(Guid libraryUid, Guid bookId, int delta);

        public Task<string> ChangeCondition(Guid bookId, string condition);
        public Task<BookInfo> GetBookByUuid(Guid bookId);
        public Task<LibraryResponse> GetLibraryByUuid(Guid libid);
        public Task<Book> GetBookConditionByUuid(Guid bookId);

    }
}
