namespace RatingService
{
    public interface IRatingService
    {
        public Task<UserRatingResponse> GetUserRating(string userName);
        public Task<UserRatingResponse> ChangeUserRating(string userName, int delta);

    }
}
