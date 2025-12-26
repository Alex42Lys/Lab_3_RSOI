using LibraryService.DTOs;
using LibraryService.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibraryService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LibraryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;

        public LibraryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }


        [HttpGet("libraries")]
        public async Task<ActionResult<LibraryPaginationResponse>> GetLibraries(
            [FromQuery] string city,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null)
        {
            try
            {
                var libraries = await _libraryService.GetLibrariesByCity(city);

                int currentPage = page ?? 1;
                int pageSize = size ?? 10;
                var pagedLibraries = libraries
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var response = new LibraryPaginationResponse
                {
                    Page = currentPage,
                    PageSize = pageSize,
                    TotalElements = libraries.Count,
                    Items = pagedLibraries,
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse { Message = ex.ToString() });
            }
        }

        [HttpGet("libraries/{libraryUid}/books")]
        public async Task<ActionResult<LibraryBookPaginationResponse>> GetLibraryBooks(
            [FromRoute] Guid libraryUid,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null,
            [FromQuery] bool? showAll = null)
        {
            try
            {
                var books = await _libraryService.GetBooksByLibrary(libraryUid, showAll ?? false);

                int currentPage = page ?? 1;
                int pageSize = size ?? 10;
                var pagedBooks = books
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var response = new LibraryBookPaginationResponse
                {
                    Page = currentPage,
                    PageSize = pageSize,
                    TotalElements = books.Count,
                    Items = pagedBooks,
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse { Message = ex.ToString() });
            }
        }
        [HttpPost("changeCount")]
        public async Task<int> ChangeCount(Guid bookId, Guid libId, int delta)
        {
            var res = await _libraryService.ChangeCount(bookId, libId, delta);
            return res;
        }

        [HttpPut("changeCondition")]
        public async Task<string> ChangeCondition(Guid bookId, string condition)
        {
            var res = await _libraryService.ChangeCondition(bookId, condition);
            return res;
        }

        [HttpGet("GetBookByUuid")]
        public async Task<BookInfo> GetBookByUuid(Guid bookId)
        {
            var res = await _libraryService.GetBookByUuid(bookId);
            return res;
        }
        [HttpGet("GetBookConditionByUuid")]
        public async Task<Book> GetBookConditionByUuid(Guid bookId)
        {
            var res = await _libraryService.GetBookConditionByUuid(bookId);
            return res;
        }
        [HttpGet("GetLibraryByUuid")]
        public async Task<LibraryResponse> GetLibraryByUuid(Guid libid)
        {
            var res = await _libraryService.GetLibraryByUuid(libid);
            return res;
        }
    }
}