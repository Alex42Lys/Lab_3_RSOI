namespace RatingService
{
    public class RatingService : IRatingService 
    {
        private IRatingRepository _repository;
        public RatingService(IRatingRepository repository)
        {
            _repository = repository;
        }

        public async Task<UserRatingResponse> GetUserRating(string userName)
        {
            var rating = await _repository.GetUserRating(userName);
            var dto = new UserRatingResponse()
            {
                Stars = rating.Stars,
            };
            return dto;
        }

        public async Task<UserRatingResponse> ChangeUserRating(string userName, int delta)
        {
            await _repository.ChangeUserRating(userName, delta);
            var rating = await _repository.GetUserRating(userName);
            var dto = new UserRatingResponse()
            {
                Stars = rating.Stars,
            };
            return dto;
        }
    }
}
