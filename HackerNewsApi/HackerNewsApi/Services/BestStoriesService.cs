using HackerNewsApi.Models;

namespace HackerNewsApi.Services
{
    public class BestStoriesService
    {
        private readonly HackerNewsClient _client;
        private readonly ILogger<BestStoriesService> _logger;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        private List<Story>? _cachedStories;
        private DateTime _nextFetchTime = DateTime.MinValue;

        public BestStoriesService(HackerNewsClient client, ILogger<BestStoriesService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<List<Story>?> GetBestStoriesAsync(int count)
        {
            await _cacheLock.WaitAsync();

            try
            {
                if (DateTime.UtcNow >= _nextFetchTime)
                {
                    await UpdateCacheAsync();
                }

                if (_cachedStories == null)
                {
                    return null;
                }

                return _cachedStories.Take(count).ToList();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task UpdateCacheAsync()
        {
            try
            {
                var items = await _client.GetBestStoriesAsync();
                var stories = new List<Story>();

                foreach (var item in items.OrderByDescending(item => item.Score).ThenBy(item => item.Id))
                {
                    var story = new Story
                    {
                        Title = item.Title,
                        Uri = item.Url,
                        PostedBy = item.By,
                        Time = DateTimeOffset.FromUnixTimeSeconds(item.Time),
                        Score = item.Score,
                        CommentCount = item.Descendants
                    };

                    if (string.IsNullOrWhiteSpace(story.Uri))
                    {
                        story.Uri = $"https://news.ycombinator.com/item?id={item.Id}";
                    }

                    stories.Add(story);
                }

                _cachedStories = stories;
                _nextFetchTime = DateTime.UtcNow.AddMinutes(1);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not fetch stories from Hacker News.");
                _cachedStories = null;

                // During an outage, don't retry Hacker News for every incoming request.
                _nextFetchTime = DateTime.UtcNow.AddSeconds(30);
            }
        }
    }
}
