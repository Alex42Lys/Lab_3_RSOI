using LibraryService.DTOs;
using LibraryService.Models;

namespace LibraryService
{
    public class LibraryService : ILibraryService
    {
        private readonly ILibraryRepository _libraryRepository;
        public LibraryService(ILibraryRepository libraryRepository) 
        {
            _libraryRepository = libraryRepository;
        }

        public async Task<List<LibraryResponse>> GetLibrariesByCity(string city)
        {
            var libs = await _libraryRepository.GetLibrariesByCity(city);
            var libRes = new List<LibraryResponse>();
            foreach (var lib in libs)
            {
                libRes.Add(new LibraryResponse
                {
                    LibraryUid = lib.LibraryUid,
                    Address = lib.Address,
                    City = lib.City,
                    Name = lib.Name,
                });
            }
            return libRes;
        }

        public async Task<List<LibraryBookResponse>> GetBooksByLibrary(Guid libraryUid, bool showAll)
        {
            var books = await _libraryRepository.GetBooksByLibrary(libraryUid);
            if (showAll)
                return books;
            else
                return books.Where(x => x.AvailableCount > 0).ToList();
        }

        public async Task<int> ChangeCount(Guid libraryUid, Guid bookId, int delta)
        {
            var newCount = await _libraryRepository.ChangeCount(libraryUid, bookId, delta);
            return newCount;
        }
        public async Task<string> ChangeCondition(Guid bookId, string condition)
        {
            var newCondition = await _libraryRepository.ChangeCondition(bookId, condition);
            return newCondition;
        }
        public async Task<BookInfo> GetBookByUuid(Guid bookId)
        {
            return await  _libraryRepository.GetBookByUuid(bookId);
        }
        public async Task<Book> GetBookConditionByUuid(Guid bookId)
        {
            return await _libraryRepository.GetBookConditionByUuid(bookId);
        }
        public async Task<LibraryResponse> GetLibraryByUuid(Guid libid)
        {
            return await _libraryRepository.GetLibraryByUuid(libid);
        }

    }
}
