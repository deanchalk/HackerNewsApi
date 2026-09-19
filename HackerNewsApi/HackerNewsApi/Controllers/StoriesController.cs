using HackerNewsApi.Models;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Controllers
{
    [ApiController]
    [Route("api/stories")]
    public class StoriesController : ControllerBase
    {
        private readonly BestStoriesService _stories;

        public StoriesController(BestStoriesService stories)
        {
            _stories = stories;
        }

        [HttpGet("best")]
        public async Task<ActionResult<List<Story>>> GetBest(int n)
        {
            if (n < 1)
            {
                return BadRequest("Please provide a positive number for n.");
            }

            var stories = await _stories.GetBestStoriesAsync(n);

            if (stories == null)
            {
                return StatusCode(503, "Hacker News is currently unavailable. Please try again later.");
            }

            return Ok(stories);
        }
    }
}
