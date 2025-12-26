using RatingService.Models;

namespace RatingService
{
    public interface IRatingRepository
    {
        public Task ChangeUserRating(string userName, int delta);
        public Task<Rating> GetUserRating(string userName);

    }
}
