using GatewayService.Controllers;
using GatewayService.DTOs;
using GatewayService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.Protected;

using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GatewayService.Tests
{
    public class LibrarySystemControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly LibrarySystemController _controller;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public LibrarySystemControllerTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            _controller = new LibrarySystemController(_httpClientFactoryMock.Object);


            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.ControllerContext.HttpContext.Request.Headers["X-User-Name"] = "testuser";
        }

        [Fact]
        public async Task TakeBook_ValidRequest_ReturnsReservationWithRating()
        {
            var takeBookRequest = new TakeBookRequest
            {
                BookUid = Guid.NewGuid().ToString(),
                LibraryUid = Guid.NewGuid().ToString(),
                TillDate = "2024-01-15"
            };

            var reservationsResponse = new List<Reservation>();
            SetupHttpResponse("http://reservation:8080/Reservation/AllReservations",
                HttpStatusCode.OK, reservationsResponse);


            var ratingResponse = new UserRatingResponse { Stars = 5 };
            SetupHttpResponse("http://rating:8080/Rating/rating",
                HttpStatusCode.OK, ratingResponse);


            var reservationResponse = new Reservation
            {
                ReservationUid = Guid.NewGuid(),
                Status = "RENTED",
                StartDate = DateTime.Now,
                TillDate = DateTime.Now.AddDays(7)
            };
            SetupHttpResponse("http://reservation:8080/Reservation/CreateNewReservation",
                HttpStatusCode.OK, reservationResponse, HttpMethod.Post);


            SetupHttpResponse($"http://library:8080/Library/changeCount?bookId={takeBookRequest.BookUid}&libId={takeBookRequest.LibraryUid}&delta={-1}",
                HttpStatusCode.OK, 1);


            var bookInfo = new BookInfo
            {
                BookUid = takeBookRequest.BookUid,
                Name = "Test Book",
                Author = "Test Author",
                Genre = "Fiction"
            };
            SetupHttpResponse($"http://library:8080/Library/GetBookByUuid?bookId={takeBookRequest.BookUid}",
                HttpStatusCode.OK, bookInfo);

            var libraryInfo = new LibraryResponse
            {
                LibraryUid =Guid.Parse(takeBookRequest.LibraryUid),
                Name = "Test Library",
                City = "Test City"
            };
            SetupHttpResponse($"http://library:8080/Library/GetLibraryByUuid?libid={takeBookRequest.LibraryUid}",
                HttpStatusCode.OK, libraryInfo);

             
            var result = await _controller.TakeBook(takeBookRequest);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<TakeBookResponse>(okResult.Value);

            Assert.Equal(reservationResponse.ReservationUid.ToString(), response.ReservationUid);
            Assert.Equal("RENTED", response.Status);
            Assert.Equal(5, response.Rating.Stars);
            Assert.Equal("Test Book", response.Book.Name);
            Assert.Equal("Test Library", response.Library.Name);
        }

        private void SetupHttpResponse(string url, HttpStatusCode statusCode, object responseContent, HttpMethod method = null)
        {
            try
            {
                method ??= HttpMethod.Get;

                var responseContentJson = JsonSerializer.Serialize(responseContent);
                var httpResponse = new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseContentJson, Encoding.UTF8, "application/json")
                };

                var u = url.Split('?')[0];

                _httpMessageHandlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == method &&
                            req.RequestUri.ToString().Contains(u)),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(httpResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupHttpResponse error for {url}: {ex.Message}");
                throw;
            }
        }
        [Fact]
        public async Task ReturnBook_BookReturnedInGoodCondition_IncreasesRating()
        {
             
            var reservationUid = Guid.NewGuid();
            var returnBookRequest = new ReturnBookRequest
            {
                Condition = "EXCELLENT",
                Date = "2024-01-10"
            };


            _controller.ControllerContext.HttpContext.Request.Headers["X-User-Name"] = "testuser";


            var reservationResponse = new Reservation
            {
                ReservationUid = reservationUid,
                Status = "RETURNED",
                BookUid = Guid.NewGuid()
            };
            SetupHttpResponse($"http://reservation:8080/Reservation/CloseReservation?resId={reservationUid}",
                HttpStatusCode.OK, reservationResponse, HttpMethod.Post);


            var book = new Book
            {
                BookUid = reservationResponse.BookUid,
                Condition = "EXCELLENT" 
            };
            SetupHttpResponse($"http://library:8080/Library/GetBookConditionByUuid?bookId={reservationResponse.BookUid}",
                HttpStatusCode.OK, book);


            SetupHttpResponse($"http://rating:8080/Rating/changeRating?delta=1",
                HttpStatusCode.OK, "OK", HttpMethod.Post);

            var result = await _controller.ReturnBook(returnBookRequest, reservationUid);


            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(204, objectResult.StatusCode);
        }

        [Fact]
        public async Task ReturnBook_BookReturnedExpired_DecreasesRating()
        {
            var reservationUid = Guid.NewGuid();
            var returnBookRequest = new ReturnBookRequest
            {
                Condition = "EXCELLENT",
                Date = "2024-01-10"
            };

            _controller.ControllerContext.HttpContext.Request.Headers["X-User-Name"] = "testuser";


            var reservationResponse = new Reservation
            {
                ReservationUid = reservationUid,
                Status = "EXPIRED", 
                BookUid = Guid.NewGuid()
            };
            SetupHttpResponse($"http://reservation:8080/Reservation/CloseReservation?resId={reservationUid}",
                HttpStatusCode.OK, reservationResponse, HttpMethod.Post);


            var book = new Book
            {
                BookUid = reservationResponse.BookUid,
                Condition = "EXCELLENT"
            };
            SetupHttpResponse($"http://library:8080/Library/GetBookConditionByUuid?bookId={reservationResponse.BookUid}",
                HttpStatusCode.OK, book);


            SetupHttpResponse($"http://rating:8080/Rating/changeRating?delta=-10",
                HttpStatusCode.OK, "OK", HttpMethod.Post);

        
            var result = await _controller.ReturnBook(returnBookRequest, reservationUid);

          
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(204, objectResult.StatusCode);
        }

        [Fact]
        public async Task ReturnBook_BookReturnedInWorseCondition_DecreasesRatingAndUpdatesCondition()
        {
            var reservationUid = Guid.NewGuid();
            var returnBookRequest = new ReturnBookRequest
            {
                Condition = "BAD",  
                Date = "2024-01-10"
            };

            _controller.ControllerContext.HttpContext.Request.Headers["X-User-Name"] = "testuser";

            var reservationResponse = new Reservation
            {
                ReservationUid = reservationUid,
                Status = "RETURNED",
                BookUid = Guid.NewGuid()
            };
            SetupHttpResponse($"http://reservation:8080/Reservation/CloseReservation?resId={reservationUid}",
                HttpStatusCode.OK, reservationResponse, HttpMethod.Post);

            var book = new Book
            {
                BookUid = reservationResponse.BookUid,
                Condition = "EXCELLENT"  
            };
            SetupHttpResponse($"http://library:8080/Library/GetBookConditionByUuid?bookId={reservationResponse.BookUid}",
                HttpStatusCode.OK, book);

            SetupHttpResponse($"http://library:8080/Library/changeCondition?bookId={book.BookUid}&condition={returnBookRequest.Condition}",
                HttpStatusCode.OK, "UPDATED");

            SetupHttpResponse($"http://rating:8080/Rating/changeRating?delta=-10",
                HttpStatusCode.OK, "OK", HttpMethod.Post);

            var result = await _controller.ReturnBook(returnBookRequest, reservationUid);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(204, objectResult.StatusCode);
        }

        [Fact]
        public async Task ReturnBook_WithoutUserNameHeader_ReturnsBadRequest()
        {
            var reservationUid = Guid.NewGuid();
            var returnBookRequest = new ReturnBookRequest
            {
                Condition = "EXCELLENT",
                Date = "2024-01-10"
            };

            _controller.ControllerContext.HttpContext.Request.Headers.Remove("X-User-Name");
             
            var result = await _controller.ReturnBook(returnBookRequest, reservationUid);

             
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("X-User-Name header is required", badRequestResult.Value);
        }
    }
}