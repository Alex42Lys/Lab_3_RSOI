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
        private readonly RabbitMQService  _rabbitMQService;

        public LibrarySystemController(
            IHttpClientFactory httpClientFactory,
            ServiceCircuitBreaker circuitBreaker,
            RabbitMQService rabbitMQService
            )
        {
            _httpClient = httpClientFactory.CreateClient();
            _circuitBreaker = circuitBreaker;
            // _rabbitMQService = rabbitMQService;

        }

        [HttpGet("libraries")]
        public async Task<ActionResult<LibraryPaginationResponse>> GetLibrariesFromGateway(
            [FromQuery] string city,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null)
        {
            const string serviceName = "LibraryService";

            if (!_circuitBreaker.HasTimeOutPassed(serviceName))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
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
                    _circuitBreaker.AddRequest(serviceName);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var libraryResponse = JsonSerializer.Deserialize<LibraryPaginationResponse>(content, options);
                _circuitBreaker.Reset(serviceName);
                return Ok(libraryResponse);
            }
            catch(HttpRequestException httpex)
            {
                _circuitBreaker.AddRequest(serviceName);
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse
                {
                    Message = ex.Message
                });
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

            if (!_circuitBreaker.HasTimeOutPassed(serviceName))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
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
                    _circuitBreaker.AddRequest(serviceName);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var libraryResponse = JsonSerializer.Deserialize<LibraryBookPaginationResponse>(content, options);
                _circuitBreaker.Reset(serviceName);

                return Ok(libraryResponse);
            }
            catch (HttpRequestException httpex)
            {
                _circuitBreaker.AddRequest(serviceName);
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse
                {
                    Message = ex.Message
                });
            }
        }

        [HttpGet("rating")]
        public async Task<ActionResult> GetUserRating()
        {
            const string serviceName = "RatingService";

            if (!_circuitBreaker.HasTimeOutPassed(serviceName))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "X-User-Name header is required"
                    });
                }

                var url = "http://rating:8080/Rating/rating";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _circuitBreaker.AddRequest(serviceName);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var ratingResponse = JsonSerializer.Deserialize<UserRatingResponse>(content, options);
                _circuitBreaker.Reset(serviceName);
                return Ok(ratingResponse);
            }
            catch (HttpRequestException ex)
            {
                _circuitBreaker.AddRequest(serviceName);
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse
                {
                    Message = ex.Message
                });
            }
        }

        [HttpGet("reservations")]
        public async Task<ActionResult> GetAllUserReservations()
        {
            const string reservationService = "ReservationService";
            const string libraryService = "LibraryService";

            if (!_circuitBreaker.HasTimeOutPassed(reservationService))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "X-User-Name header is required"
                    });
                }

                var url = "http://reservation:8080/Reservation/AllReservations";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _circuitBreaker.AddRequest(reservationService);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
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
                    if (!_circuitBreaker.HasTimeOutPassed(libraryService))
                    {
                        return StatusCode(503, new ErrorResponse
                        {
                            Message = "Bonus Service unavailable"
                        });
                    }

                    {
                        var bookId = reservation.BookUid;
                        var libId = reservation.LibraryUid;
                        try
                        {


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
                        catch (HttpRequestException ex) 
                        {
                            _circuitBreaker.AddRequest(libraryService);
                            return StatusCode(503, new ErrorResponse
                            {
                                Message = "Bonus Service unavailable"
                            });
                        }
                    }
                }
                _circuitBreaker.Reset(libraryService);

                _circuitBreaker.Reset(reservationService);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse
                {
                    Message = ex.Message
                });
            }
        }

        [HttpPost("reservations")]
        public async Task<ActionResult> TakeBook([FromBody] TakeBookRequest takeBookRequest)
        {
            const string reservationService = "ReservationService";
            const string ratingService = "RatingService";
            const string libraryService = "LibraryService";

            if (!_circuitBreaker.HasTimeOutPassed(reservationService))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            if (!_circuitBreaker.HasTimeOutPassed(ratingService))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            if (!_circuitBreaker.HasTimeOutPassed(libraryService))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "X-User-Name header is required"
                    });
                }
                HttpResponseMessage? response = null;
                string? url = "";
                HttpRequestMessage? request = null;
                try
                {
                    url = "http://reservation:8080/Reservation/AllReservations";
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("X-User-Name", username.ToString());

                    response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        _circuitBreaker.AddRequest(reservationService);
                        return StatusCode(503, new ErrorResponse
                        {
                            Message = "Bonus Service unavailable"
                        });
                    }
                }
                catch (HttpRequestException ex)
                {
                    _circuitBreaker.AddRequest(reservationService);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var reservations = JsonSerializer.Deserialize<List<Reservation>>(content, options);
                var bookCount = reservations.Count();

                url = "http://rating:8080/Rating/rating";
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-User-Name", username.ToString());

                response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _circuitBreaker.AddRequest(ratingService);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }

                content = await response.Content.ReadAsStringAsync();

                var ratingResponse = JsonSerializer.Deserialize<UserRatingResponse>(content, options);
                if (ratingResponse.Stars <= bookCount)
                {
                    return StatusCode(403, new ErrorResponse
                    {
                        Message = "Too many rented books"
                    });
                }
                Reservation reservationResponse = null;
                try
                {
                    url = "http://reservation:8080/Reservation/CreateNewReservation";
                    var json = JsonSerializer.Serialize(takeBookRequest);
                    var reqContent = new StringContent(json, Encoding.UTF8, "application/json");

                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMessage.Headers.Add("X-User-Name", username.ToString());
                    requestMessage.Content = reqContent;

                    var respContent = await _httpClient.SendAsync(requestMessage);

                    if (!respContent.IsSuccessStatusCode)
                    {
                        _circuitBreaker.AddRequest(reservationService);
                        return StatusCode(503, new ErrorResponse
                        {
                            Message = "Bonus Service unavailable"
                        });
                    }
                    var responseContent = await respContent.Content.ReadAsStringAsync();
                    reservationResponse = JsonSerializer.Deserialize<Reservation>(responseContent, options);
                }
                catch (HttpRequestException ex)
                {
                    _circuitBreaker.AddRequest(reservationService);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }
                TakeBookResponse ans = null;
                try
                {


                    url = $"http://library:8080/Library/changeCount?bookId={takeBookRequest.BookUid}libId={takeBookRequest.LibraryUid}delta={-1}";
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    response = await _httpClient.SendAsync(request);
                    content = await response.Content.ReadAsStringAsync();

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

                    ans = new TakeBookResponse()
                    {
                        Book = bookInfo,
                        Library = libraryInfo,
                        ReservationUid = reservationResponse.ReservationUid.ToString(),
                        Status = reservationResponse.Status,
                        StartDate = reservationResponse.StartDate.ToString("yyyy-MM-dd"),
                        TillDate = reservationResponse.TillDate.ToString("yyyy-MM-dd"),
                        Rating = ratingResponse,
                    };
                }
                catch (HttpRequestException ex)
                {
                    _circuitBreaker.AddRequest(libraryService);
                    url = $"http://library:8080/Reservations/DeleteReservation?id={reservationResponse.ReservationUid.ToString()}";
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    response = await _httpClient.SendAsync(request);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }
                _circuitBreaker.Reset(reservationService);
                _circuitBreaker.Reset(ratingService);
                return Ok(ans);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }
        }

        [HttpPost("reservations/{reservationUid}/return")]
        public async Task<ActionResult> ReturnBook(
            [FromBody] ReturnBookRequest returnBookRequest,
            [FromRoute] Guid reservationUid)
        {
            const string reservationService = "ReservationService";
            const string ratingService = "RatingService";
            const string libraryService = "LibraryService";

            // Проверяем доступность всех сервисов
            if (!_circuitBreaker.HasTimeOutPassed(reservationService) ||
                !_circuitBreaker.HasTimeOutPassed(ratingService) ||
                !_circuitBreaker.HasTimeOutPassed(libraryService))
            {
                return StatusCode(503, new ErrorResponse
                {
                    Message = "Bonus Service unavailable"
                });
            }

            try
            {
                if (!Request.Headers.TryGetValue("X-User-Name", out var username))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "X-User-Name header is required"
                    });
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                // 1. Закрытие резервации
                var url = $"http://reservation:8080/Reservation/CloseReservation?resId={reservationUid.ToString()}";
                var json = JsonSerializer.Serialize(returnBookRequest);
                var reqContent = new StringContent(json, Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Headers.Add("X-User-Name", username.ToString());
                requestMessage.Content = reqContent;

                var respContent = await _httpClient.SendAsync(requestMessage);

                if (!respContent.IsSuccessStatusCode)
                {
                    _circuitBreaker.AddRequest(reservationService);
                    return StatusCode(503, new ErrorResponse
                    {
                        Message = "Bonus Service unavailable"
                    });
                }

                var responseContent = await respContent.Content.ReadAsStringAsync();
                var reservationResponse = JsonSerializer.Deserialize<Reservation>(responseContent, options);

                var bookId = reservationResponse.BookUid;
                var libraryUid = reservationResponse.LibraryUid;
                var deltaRating = 0;

                try
                {
                    // 2. Получение информации о состоянии книги
                    var bookUrl = $"http://library:8080/Library/GetBookConditionByUuid?bookId={bookId}";
                    var bookRequest = new HttpRequestMessage(HttpMethod.Get, bookUrl);
                    var bookResponse = await _httpClient.SendAsync(bookRequest);

                    if (bookResponse.IsSuccessStatusCode)
                    {
                        var bookContent = await bookResponse.Content.ReadAsStringAsync();
                        var book = JsonSerializer.Deserialize<Book>(bookContent, options);

                        if (reservationResponse.Status == "RETURNED" && returnBookRequest.Condition == book.Condition)
                        {
                            deltaRating += 1;
                        }
                        if (reservationResponse.Status == "EXPIRED")
                        {
                            deltaRating -= 10;
                        }
                        if (returnBookRequest.Condition != book.Condition)
                        {
                            deltaRating -= 10;
                            // Обновление состояния книги
                            await UpdateBookCondition(book.BookUid, returnBookRequest.Condition);
                        }
                    }
                    else
                    {
                        // Если не удалось получить состояние книги, добавляем в очередь
                        await QueueUpdateBookCondition(bookId, returnBookRequest.Condition, deltaRating);
                    }
                }
                catch (Exception ex)
                {
                    // Если не удалось получить состояние книги, добавляем в очередь
                    await QueueUpdateBookCondition(bookId, returnBookRequest.Condition, deltaRating);
                }

                // 3. Увеличение количества доступных книг
                var bookCountSuccess = await IncreaseBookCount(libraryUid, bookId);
                if (!bookCountSuccess)
                {
                    // Ставим в очередь для повторной попытки
                    QueueIncreaseBookCount(libraryUid, bookId);
                }

                // 4. Изменение рейтинга пользователя (только если deltaRating != 0)
                if (deltaRating != 0)
                {
                    var ratingSuccess = await UpdateRating(username.ToString(), deltaRating);
                    if (!ratingSuccess)
                    {
                        // Ставим в очередь для повторной попытки
                        QueueUpdateRating(username.ToString(), deltaRating);
                    }
                }

                // Сбрасываем circuit breaker для сервисов
                _circuitBreaker.Reset(reservationService);
                _circuitBreaker.Reset(ratingService);
                _circuitBreaker.Reset(libraryService);

                // Возвращаем успех даже если фоновые операции в очереди
                return StatusCode(204);
            }
            catch (Exception ex)
            {
                _circuitBreaker.AddRequest(reservationService);
                return StatusCode(503, new ErrorResponse
                {
                    Message = ex.Message
                });
            }
        }

        // Основной метод для обновления состояния книги (без таймаута при первом вызове)
        private async Task<bool> UpdateBookCondition(Guid bookUid, string condition)
        {
            try
            {
                var url = $"http://library:8080/Library/changeCondition?bookId={bookUid}&condition={condition}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // Не удалось - ставим в очередь для повторной попытки с таймаутом
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обновлении состояния книги: {ex.Message}");
                return false;
            }
        }

        // Метод для постановки обновления состояния книги в очередь
        private async Task QueueUpdateBookCondition(Guid bookUid, string condition, int deltaRating)
        {
            var queueMessage = new
            {
                Type = "UpdateBookCondition",
                BookUid = bookUid,
                Condition = condition,
                DeltaRating = deltaRating, // Сохраняем deltaRating для обновления рейтинга
                Timestamp = DateTime.UtcNow,
                RetryCount = 0
            };

            _rabbitMQService.SendMessage(queueMessage);
        }

        // Основной метод для увеличения количества книг (без таймаута при первом вызове)
        private async Task<bool> IncreaseBookCount(Guid libraryUid, Guid bookUid)
        {
            try
            {
                var url = $"http://library:8080/Library/changeCount?bookId={bookUid}&libId={libraryUid}&delta=1";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // Не удалось - возвращаем false для постановки в очередь
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при увеличении количества книг: {ex.Message}");
                return false;
            }
        }

        // Метод для постановки увеличения количества книг в очередь
        private void QueueIncreaseBookCount(Guid libraryUid, Guid bookUid)
        {
            var queueMessage = new
            {
                Type = "IncreaseBookCount",
                BookUid = bookUid,
                LibraryUid = libraryUid,
                Delta = 1,
                Timestamp = DateTime.UtcNow,
                RetryCount = 0
            };

            _rabbitMQService.SendMessage(queueMessage);
        }

        // Основной метод для обновления рейтинга (без таймаута при первом вызове)
        private async Task<bool> UpdateRating(string username, int deltaRating)
        {
            try
            {
                var url = $"http://rating:8080/Rating/changeRating?delta={deltaRating}";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-User-Name", username);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // Не удалось - возвращаем false для постановки в очередь
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обновлении рейтинга: {ex.Message}");
                return false;
            }
        }

        // Метод для постановки обновления рейтинга в очередь
        private void QueueUpdateRating(string username, int deltaRating)
        {
            var queueMessage = new
            {
                Type = "UpdateRating",
                UserName = username,
                DeltaRating = deltaRating,
                Timestamp = DateTime.UtcNow,
                RetryCount = 0
            };

            _rabbitMQService.SendMessage(queueMessage);
        }

    }
}