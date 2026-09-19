using System.Net;
using System.Net.Http.Json;
using HackerNewsApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNewsApi.Tests
{
    // Runs the real API, but replaces calls to Hacker News with local test data.
    public class TestApplication : WebApplicationFactory<Program>
    {
        public FakeHackerNewsHandler HackerNews { get; } = new FakeHackerNewsHandler();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("HackerNews")
                    .ConfigurePrimaryHttpMessageHandler(() => HackerNews);
            });
        }
    }

    public class FakeHackerNewsHandler : HttpMessageHandler
    {
        public List<long> Ids { get; set; } = new List<long> { 1, 3, 2 };
        public Dictionary<long, HackerNewsItem?> Items { get; } = new Dictionary<long, HackerNewsItem?>();
        public bool Fail { get; set; }
        public int RequestCount;

        public FakeHackerNewsHandler()
        {
            for (int id = 1; id <= 3; id++)
            {
                Items[id] = new HackerNewsItem
                {
                    Id = id,
                    Type = "story",
                    Title = $"Story {id}",
                    By = "author",
                    Time = 1570887781,
                    Score = id == 1 ? 1 : 100,
                    Descendants = 12,
                    Url = id == 2 ? "" : "https://example.com/story"
                };
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref RequestCount);
            // A short delay lets concurrent API requests overlap in the cache test.
            await Task.Delay(5, token);

            if (Fail)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("beststories.json"))
            {
                response.Content = JsonContent.Create(Ids);
            }
            else
            {
                var id = long.Parse(Path.GetFileNameWithoutExtension(path));
                response.Content = JsonContent.Create(Items[id]);
            }

            return response;
        }
    }
}
