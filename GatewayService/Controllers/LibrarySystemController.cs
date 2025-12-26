using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using GatewayService.DTOs;
using GatewayService.Models;
using System.Text;
using Microsoft.Extensions.Options;
using System;
namespace GatewayService.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class LibrarySystemController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        // Inject IHttpClientFactory instead of creating HttpClient directly
        public LibrarySystemController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }
        [HttpGet("libraries")]
        public async Task<ActionResult<LibraryPaginationResponse>> GetLibrariesFromGateway(
        [FromQuery] string city,
        [FromQuery] int? page = null,
        [FromQuery] int? size = null)
        {
            try
            {
                var uriBuilder = new UriBuilder($"http://library:8080/Library/libraries");
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                if (!string.IsNullOrEmpty(city))
                    query["city"] = city;
                if (page.HasValue)
                    query["page"] = page.Value.ToString();
                if (size.HasValue)
                    query["size"] = size.Value.ToString();

                uriBuilder.Query = query.ToString();
                var url = uriBuilder.ToString();


                var response = await _httpClient.GetAsync(url);


                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching libraries");
                }

                var content = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var libraryResponse = JsonSerializer.Deserialize<LibraryPaginationResponse>(content, options);

                return Ok(libraryResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
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
                var uriBuilder = new UriBuilder($"http://library:8080/Library/libraries/{libraryUid}/books");
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                if (showAll.HasValue)
                    query["showAll"] = showAll.ToString();
                if (page.HasValue)
                    query["page"] = page.Value.ToString();
                if (size.HasValue)
                    query["size"] = size.Value.ToString();

                uriBuilder.Query = query.ToString();
                var url = uriBuilder.ToString();


                var response = await _httpClient.GetAsync(url);


                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching libraries");
                }

                var content = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var libraryResponse = JsonSerializer.Deserialize<LibraryBookPaginationResponse>(content, options);

                return Ok(libraryResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpGet("rating")]
        public async Task<ActionResult> GetUserRating()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest("X-User-Name header is required");
                }

                var url = "http://rating:8080/Rating/rating";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching rating");
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var ratingResponse = JsonSerializer.Deserialize<UserRatingResponse>(content, options);

                return Ok(ratingResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpGet("reservations")]
        public async Task<ActionResult> GetAllUserReservations()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest("X-User-Name header is required");
                }

                var url = "http://reservation:8080/Reservation/AllReservations";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching reservations");
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                Console.WriteLine("GOT HERE");
                var reservations = JsonSerializer.Deserialize<List<Reservation>>(content, options);

                var result = new List<BookReservationResponse>();

                foreach (var reservation in reservations)
                {
                    var bookId = reservation.BookUid;
                    var libId = reservation.LibraryUid;

                    var bookUrl = $"http://library:8080/Library/GetBookByUuid?bookId={bookId}";
                    var bookRequest = new HttpRequestMessage(HttpMethod.Get, bookUrl);
                    var bookResponse = await _httpClient.SendAsync(bookRequest);


                    var bookContent = await bookResponse.Content.ReadAsStringAsync();
                    var bookInfo = JsonSerializer.Deserialize<BookInfo>(bookContent, options);

                    var libUrl = $"http://library:8080/Library/GetLibraryByUuid?libid={libId}";
                    var libRequest = new HttpRequestMessage(HttpMethod.Get, libUrl);
                    var libResponse = await _httpClient.SendAsync(libRequest);


                    var libContent = await libResponse.Content.ReadAsStringAsync();
                    var libraryInfo = JsonSerializer.Deserialize<LibraryResponse>(libContent, options);

                    var reservationResponse = new BookReservationResponse()
                    {
                        Book = bookInfo,
                        Library = libraryInfo,
                        ReservationUid = reservation.ReservationUid.ToString(),
                        StartDate = reservation.StartDate.ToString("yyyy-MM-dd"),
                        Status = reservation.Status,
                        TillDate = reservation.TillDate.ToString("yyyy-MM-dd"),
                    };

                    result.Add(reservationResponse);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost("reservations")]
        public async Task<ActionResult> TakeBook([FromBody] TakeBookRequest takeBookRequest)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest("X-User-Name header is required");
                }

                var url = "http://reservation:8080/Reservation/AllReservations";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching rating");
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var reservations = JsonSerializer.Deserialize<List<Reservation>>(content, options);
                var bookCount = reservations.Count();

                //////////////////////////////////////////////

                url = "http://rating:8080/Rating/rating";
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Error fetching rating");
                }

                content = await response.Content.ReadAsStringAsync();

                var ratingResponse = JsonSerializer.Deserialize<UserRatingResponse>(content, options);
                if(ratingResponse.Stars <= bookCount)
                {
                    return StatusCode(403, "Too many rented books");

                }

                /////////////////////////////////////////////////


                url = "http://reservation:8080/Reservation/CreateNewReservation";
                var json = JsonSerializer.Serialize(takeBookRequest);
                var reqContent = new StringContent(json, Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Headers.Add("X-User-Name", username.ToString());
                requestMessage.Content = reqContent;

                var respContent = await _httpClient.SendAsync(requestMessage);

                if (!respContent.IsSuccessStatusCode)
                {
                    return StatusCode((int)respContent.StatusCode, "Error creating reservation");
                }
                var responseContent = await respContent.Content.ReadAsStringAsync();
                var reservationResponse = JsonSerializer.Deserialize<Reservation>(responseContent, options);

                //////////////////////////////////////////////////////

                url = $"http://library:8080/Library/changeCount?bookId={takeBookRequest.BookUid}libId={takeBookRequest.LibraryUid}delta={-1}";
                request = new HttpRequestMessage(HttpMethod.Get, url);
                response = await _httpClient.SendAsync(request);
                content = await response.Content.ReadAsStringAsync();
               // var changeCountResponse = JsonSerializer.Deserialize<int>(content, options);

                //////////////////////////////////////////////
                var bookId = takeBookRequest.BookUid;
                var libId = takeBookRequest.LibraryUid;

                var bookUrl = $"http://library:8080/Library/GetBookByUuid?bookId={bookId}";
                var bookRequest = new HttpRequestMessage(HttpMethod.Get, bookUrl);
                var bookResponse = await _httpClient.SendAsync(bookRequest);


                var bookContent = await bookResponse.Content.ReadAsStringAsync();
                var bookInfo = JsonSerializer.Deserialize<BookInfo>(bookContent, options);

                var libUrl = $"http://library:8080/Library/GetLibraryByUuid?libid={libId}";
                var libRequest = new HttpRequestMessage(HttpMethod.Get, libUrl);
                var libResponse = await _httpClient.SendAsync(libRequest);


                var libContent = await libResponse.Content.ReadAsStringAsync();
                var libraryInfo = JsonSerializer.Deserialize<LibraryResponse>(libContent, options);
                /////////////

                var ans = new TakeBookResponse()
                {
                    Book = bookInfo,
                    Library = libraryInfo,
                    ReservationUid = reservationResponse.ReservationUid.ToString(),
                    Status = reservationResponse.Status,
                    StartDate = reservationResponse.StartDate.ToString("yyyy-MM-dd"),
                    TillDate = reservationResponse.TillDate.ToString("yyyy-MM-dd"),
                    Rating = ratingResponse,
                };

                return Ok(ans);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("reservations/{reservationUid}/return")]
        public async Task<ActionResult> ReturnBook([FromBody] ReturnBookRequest returnBookRequest, [FromRoute] Guid reservationUid)
        {
            try
            {


                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest("X-User-Name header is required");
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var url = $"http://reservation:8080/Reservation/CloseReservation?resId={reservationUid.ToString()}";
                var json = JsonSerializer.Serialize(returnBookRequest);
                var reqContent = new StringContent(json, Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Headers.Add("X-User-Name", username.ToString());
                requestMessage.Content = reqContent;

                var respContent = await _httpClient.SendAsync(requestMessage);

                if (!respContent.IsSuccessStatusCode)
                {
                    return StatusCode((int)respContent.StatusCode, await respContent.Content.ReadAsStringAsync());
                }
                var responseContent = await respContent.Content.ReadAsStringAsync();
                var reservationResponse = JsonSerializer.Deserialize<Reservation>(responseContent, options);


                //////


                var bookId = reservationResponse.BookUid;

                var bookUrl = $"http://library:8080/Library/GetBookConditionByUuid?bookId={bookId}";
                var bookRequest = new HttpRequestMessage(HttpMethod.Get, bookUrl);
                var bookResponse = await _httpClient.SendAsync(bookRequest);


                var bookContent = await bookResponse.Content.ReadAsStringAsync();
                var book = JsonSerializer.Deserialize<Book>(bookContent, options);

                ////
                ///
                var deltaRating = 0;
                if (reservationResponse.Status == "RETURNED" && returnBookRequest.Condition == book.Condition)
                {
                    deltaRating += 1;
                }
                if (reservationResponse.Status == "EXPIRED") 
                {
                    deltaRating -= 10;
                }
                if(returnBookRequest.Condition != book.Condition)
                {
                    deltaRating -= 10;
                    url = $"http://library:8080/Library/changeCondition?bookId={book.BookUid}condition={returnBookRequest.Condition}";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    var response = await _httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    var changeConditionresponse = JsonSerializer.Deserialize<string>(content, options);
                }

                url = $"http://rating:8080/Rating/changeRating?delta={deltaRating}";
                var re = new HttpRequestMessage(HttpMethod.Post, url);
                re.Headers.Add("X-User-Name", username.ToString());
                var rrp = await _httpClient.SendAsync(re);
                //var content = await response.Content.ReadAsStringAsync();


                return StatusCode(204, "Книга успешно возвращена");

            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
