using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNewsApi.Tests
{
    public class ApiTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("?n=0")]
        [InlineData("?n=-1")]
        [InlineData("?n=abc")]
        [InlineData("?n=2147483648")]
        public async Task InvalidCountReturnsBadRequest(string query)
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();

            var response = await client.GetAsync("/api/stories/best" + query);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task ReturnsHighestScoresAndCorrectFields()
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();

            var stories = await client.GetFromJsonAsync<List<Story>>("/api/stories/best?n=2");

            Assert.NotNull(stories);
            Assert.Equal(2, stories.Count);
            Assert.Equal("Story 2", stories[0].Title);
            Assert.Equal("Story 3", stories[1].Title);
            Assert.Equal(100, stories[0].Score);
            Assert.Equal("author", stories[0].PostedBy);
            Assert.Equal("https://news.ycombinator.com/item?id=2", stories[0].Uri);
            Assert.Equal("https://example.com/story", stories[1].Uri);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1570887781), stories[0].Time);
            Assert.Equal(12, stories[0].CommentCount);
            Assert.Equal(4, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task ResponseUsesRequiredJsonFieldNames()
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();

            var json = await client.GetFromJsonAsync<JsonElement>("/api/stories/best?n=1");
            var fields = json[0].EnumerateObject().Select(field => field.Name).ToArray();

            Assert.Equal(new[] { "title", "uri", "postedBy", "time", "score", "commentCount" }, fields);
        }

        [Fact]
        public async Task CachedRequestsDoNotFetchAgain()
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();

            await client.GetAsync("/api/stories/best?n=1");
            var stories = await client.GetFromJsonAsync<List<Story>>("/api/stories/best?n=100");

            Assert.Equal(3, stories!.Count);
            Assert.Equal(4, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task SimultaneousRequestsOnlyFetchOneCopy()
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();
            var requests = new List<Task<HttpResponseMessage>>();

            for (int i = 0; i < 100; i++)
            {
                requests.Add(client.GetAsync("/api/stories/best?n=2"));
            }

            var responses = await Task.WhenAll(requests);

            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                response.Dispose();
            }

            Assert.Equal(4, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task SkipsUnavailableStoriesAndDuplicateIds()
        {
            using var app = new TestApplication();
            app.HackerNews.Ids = new List<long> { 1, 1, 2, 3, 4, 5 };
            app.HackerNews.Items[2] = null;
            app.HackerNews.Items[3]!.Deleted = true;
            app.HackerNews.Items[4] = new HackerNewsItem { Id = 4, Type = "story", Dead = true };
            app.HackerNews.Items[5] = new HackerNewsItem { Id = 5, Type = "comment" };
            using var client = app.CreateClient();

            var stories = await client.GetFromJsonAsync<List<Story>>("/api/stories/best?n=10");

            Assert.Equal("Story 1", Assert.Single(stories!).Title);
            Assert.Equal(6, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task EmptyFeedReturnsEmptyArray()
        {
            using var app = new TestApplication();
            app.HackerNews.Ids.Clear();
            using var client = app.CreateClient();

            var stories = await client.GetFromJsonAsync<List<Story>>("/api/stories/best?n=5");

            Assert.Empty(stories!);
        }

        [Fact]
        public async Task UpstreamFailureReturnsUnavailableAndDoesNotRetryImmediately()
        {
            using var app = new TestApplication();
            app.HackerNews.Fail = true;
            using var client = app.CreateClient();

            var first = await client.GetAsync("/api/stories/best?n=5");
            var second = await client.GetAsync("/api/stories/best?n=5");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
            Assert.Equal(1, app.HackerNews.RequestCount);
        }

        [Fact]
        public async Task SwaggerDescribesCountAsAnInteger()
        {
            using var app = new TestApplication();
            using var client = app.CreateClient();

            var json = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
            var operation = json.GetProperty("paths").GetProperty("/api/stories/best").GetProperty("get");
            var parameter = operation.GetProperty("parameters")[0];

            Assert.Equal("n", parameter.GetProperty("name").GetString());
            Assert.Equal("integer", parameter.GetProperty("schema").GetProperty("type").GetString());
            var swagger = await client.GetAsync("/swagger/index.html");
            Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        }
    }
}
