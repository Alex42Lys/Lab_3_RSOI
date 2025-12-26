using LibraryService.DTOs;
using LibraryService.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryService
{
    public interface ILibraryRepository
    {
        public Task<List<Library>> GetLibrariesByCity(string city);
        public Task<int> ChangeCount(Guid bookId, Guid libId, int count);
        public Task<List<LibraryBookResponse>> GetBooksByLibrary(Guid lib);
        public Task<string> ChangeCondition(Guid bookId, string condition);
        public Task<BookInfo> GetBookByUuid(Guid bookId);
        public Task<LibraryResponse> GetLibraryByUuid(Guid libid);
        public Task<Book> GetBookConditionByUuid(Guid bookId);


    }
}
