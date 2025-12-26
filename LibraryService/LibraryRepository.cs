using LibraryService.DTOs;
using LibraryService.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Xml.Linq;

namespace LibraryService
{
    public class LibraryRepository(PostgresContext _context) : ILibraryRepository
    {
        public async Task<List<Library>> GetLibrariesByCity(string city)
        {
            var libs = _context.Libraries.Where(x => x.City == city);
            return await libs.ToListAsync();
        }
        public async Task<List<LibraryBookResponse>> GetBooksByLibrary(Guid lib)
        {
            var libId = (_context.Libraries.Where(x => x.LibraryUid == lib)).FirstOrDefault();
            if (libId == null)
                throw new Exception("No library found");
            var booksWithCount = await _context.LibraryBooks.Where(x => x.LibraryId == libId.Id).ToListAsync();
            var bookResponses = new List<LibraryBookResponse>();
            var bookIds = booksWithCount.Select(y => y.BookId).ToList();
            var books = await _context.Books.Where(x => bookIds.Contains(x.Id)).ToListAsync();
            foreach (var book in booksWithCount)
            {
                bookResponses.Add(new LibraryBookResponse
                {
                    Author = books.First(x => x.Id == book.BookId).Author,
                    BookUid = books.First(x => x.Id == book.BookId).BookUid,
                    Genre = books.First(x => x.Id == book.BookId).Genre,
                    AvailableCount = book.AvailableCount,
                    Name = books.First(x => x.Id == book.BookId).Name,
                    Condition = books.First(x => x.Id == book.BookId).Condition,
                });
            }
            return bookResponses;
        }

        public async Task<int> ChangeCount(Guid bookId, Guid libId, int delta)
        {
            var lib = await _context.Libraries.Where(x => x.LibraryUid == libId).FirstOrDefaultAsync();
            var book = await _context.Books.Where(x => x.BookUid == bookId).FirstOrDefaultAsync();

            var lb = await _context.LibraryBooks.Where(x => x.LibraryId == lib.Id && x.BookId == book.Id).FirstOrDefaultAsync();
            var newCount = lb.AvailableCount + delta;
            if (newCount < 0)
                throw new Exception("Last book was already taken");
            lb.AvailableCount = newCount;
            await _context.SaveChangesAsync();
            var lb2 = await _context.LibraryBooks.Where(x => x.LibraryId == lib.Id && x.BookId == book.Id).FirstOrDefaultAsync();
            return lb.AvailableCount;

        }
        public async Task<string> ChangeCondition(Guid bookId, string condition)
        {
            var book = await _context.Books.Where(x => x.BookUid == bookId).FirstOrDefaultAsync();
            book.Condition = condition;
            await _context.SaveChangesAsync();
            var book2 = await _context.Books.Where(x => x.BookUid == bookId).FirstOrDefaultAsync();
            return book2.Condition;


        }
        public async Task<BookInfo> GetBookByUuid(Guid bookId)
        {
            var res = await _context.Books.Where(x => x.BookUid == bookId).FirstOrDefaultAsync();
            var bookInfo = new BookInfo()
            {
                Author = res.Author,
                BookUid = res.BookUid.ToString(),
                Genre = res.Genre,
                Name = res.Name,
            };
            return bookInfo;
        }

        public async Task<Book> GetBookConditionByUuid(Guid bookId)
        {
            var res = await _context.Books.Where(x => x.BookUid == bookId).FirstOrDefaultAsync();
            return res;
        }
        public async Task<LibraryResponse> GetLibraryByUuid(Guid libid)
        {
            var res = await _context.Libraries.Where(x => x.LibraryUid == libid).FirstOrDefaultAsync();
            var libInfo = new LibraryResponse()
            {
                Address = res.Address,
                City = res.City,
                LibraryUid = res.LibraryUid,
                Name = res.Name,
            };
            return libInfo;
        }
    }
}
