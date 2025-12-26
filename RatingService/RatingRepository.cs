using Microsoft.EntityFrameworkCore;
using RatingService.Models;

namespace RatingService
{
    public class RatingRepository(PostgresContext _context) : IRatingRepository
    {
        public async Task<Rating> GetUserRating(string userName)
        {
            var userRating = await _context.Ratings.Where(x => x.Username == userName).FirstOrDefaultAsync();
            return userRating;
        }

        public async Task ChangeUserRating(string userName, int delta)
        {
            var rating = await _context.Ratings
                .FirstOrDefaultAsync(x => x.Username == userName);

            if (rating == null)
            {
                throw new ArgumentException($"User {userName} not found");
            }

            var newStars = rating.Stars + delta;
            if (newStars < 0)
                newStars = 1;
            else if (newStars > 100)
                newStars = 100;


            rating.Stars = newStars;
            await _context.SaveChangesAsync();
        }
    }
}
