using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using GatewayService.DTOs;
using GatewayService.Models;
using System.Text;
using GatewayService.Services;

namespace GatewayService.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class LibrarySystemController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ServiceCircuitBreaker _circuitBreaker;

        public LibrarySystemController(
            IHttpClientFactory httpClientFactory,
            ServiceCircuitBreaker circuitBreaker)
        {
            _httpClient = httpClientFactory.CreateClient();
            _circuitBreaker = circuitBreaker;
        }

        [HttpGet("libraries")]
        public async Task<ActionResult<LibraryPaginationResponse>> GetLibrariesFromGateway(
            [FromQuery] string city,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null)
        {
            const string serviceName = "LibraryService";

            if (!_circuitBreaker.TryStartRequest(serviceName))
            {
                return StatusCode(503, new
                {
                    message = "Library service is temporarily unavailable",
                    service = serviceName
                });
            }

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
            finally
            {
                _circuitBreaker.EndRequest(serviceName);
            }
        }

        [HttpGet("libraries/{libraryUid}/books")]
        public async Task<ActionResult<LibraryBookPaginationResponse>> GetLibraryBooks(
            [FromRoute] Guid libraryUid,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null,
            [FromQuery] bool? showAll = null)
        {
            const string serviceName = "LibraryService";

            if (!_circuitBreaker.TryStartRequest(serviceName))
            {
                return StatusCode(503, "Library service is temporarily unavailable");
            }

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
            finally
            {
                _circuitBreaker.EndRequest(serviceName);
            }
        }

        [HttpGet("rating")]
        public async Task<ActionResult> GetUserRating()
        {
            const string serviceName = "RatingService";

            if (!_circuitBreaker.TryStartRequest(serviceName))
            {
                return StatusCode(503, "Rating service is temporarily unavailable");
            }

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
            finally
            {
                _circuitBreaker.EndRequest(serviceName);
            }
        }

        [HttpGet("reservations")]
        public async Task<ActionResult> GetAllUserReservations()
        {
            const string reservationService = "ReservationService";
            const string libraryService = "LibraryService";

            if (!_circuitBreaker.TryStartRequest(reservationService))
            {
                return StatusCode(503, "Reservation service is temporarily unavailable");
            }

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

                var reservations = JsonSerializer.Deserialize<List<Reservation>>(content, options);
                var result = new List<BookReservationResponse>();

                foreach (var reservation in reservations)
                {
                    if (!_circuitBreaker.TryStartRequest(libraryService))
                    {
                        continue;
                    }

                    try
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
                    finally
                    {
                        _circuitBreaker.EndRequest(libraryService);
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                _circuitBreaker.EndRequest(reservationService);
            }
        }

        [HttpPost("reservations")]
        public async Task<ActionResult> TakeBook([FromBody] TakeBookRequest takeBookRequest)
        {
            const string reservationService = "ReservationService";
            const string ratingService = "RatingService";
            const string libraryService = "LibraryService";

            //if (!_circuitBreaker.TryStartRequest(reservationService))
            //{
            //    return StatusCode(503, "Reservation service is temporarily unavailable");
            //}

            //if (!_circuitBreaker.TryStartRequest(ratingService))
            //{
            //    _circuitBreaker.EndRequest(reservationService);
            //    return StatusCode(503, "Rating service is temporarily unavailable");
            //}

            //if (!_circuitBreaker.TryStartRequest(libraryService))
            //{
            //    _circuitBreaker.EndRequest(reservationService);
            //    _circuitBreaker.EndRequest(ratingService);
            //    return StatusCode(503, "Library service is temporarily unavailable");
            //}

            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest("X-User-Name header is required");
                }

                // Оригинальный код метода
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
                if (ratingResponse.Stars <= bookCount)
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
            finally
            {
                _circuitBreaker.EndRequest(reservationService);
                _circuitBreaker.EndRequest(ratingService);
                _circuitBreaker.EndRequest(libraryService);
            }
        }

        [HttpPost("reservations/{reservationUid}/return")]
        public async Task<ActionResult> ReturnBook(
            [FromBody] ReturnBookRequest returnBookRequest,
            [FromRoute] Guid reservationUid)
        {
            const string reservationService = "ReservationService";

            //if (!_circuitBreaker.TryStartRequest(reservationService))
            //{
            //    return StatusCode(503, "Reservation service is temporarily unavailable");
            //}

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

                // Остальной оригинальный код метода
                // ...

                return StatusCode(204, "Книга успешно возвращена");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                _circuitBreaker.EndRequest(reservationService);
            }
        }
    }
}