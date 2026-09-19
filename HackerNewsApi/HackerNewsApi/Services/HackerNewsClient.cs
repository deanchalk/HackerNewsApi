using HackerNewsApi.Models;

namespace HackerNewsApi.Services
{
    public class HackerNewsClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HackerNewsClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<HackerNewsItem>> GetBestStoriesAsync()
        {
            using var client = _httpClientFactory.CreateClient("HackerNews");
            var ids = await client.GetFromJsonAsync<List<long>>("beststories.json");

            if (ids == null)
            {
                throw new HttpRequestException("Hacker News did not return a story list.");
            }

            var stories = new List<HackerNewsItem>();

            foreach (var id in ids.Distinct())
            {
                var item = await client.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json");

                if (item != null && item.Type == "story" && !item.Deleted && !item.Dead)
                {
                    stories.Add(item);
                }
            }

            return stories;
        }
    }
}
