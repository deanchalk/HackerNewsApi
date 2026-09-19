# Hacker News API

A small ASP.NET Core API that returns the highest-scoring stories from the Hacker News
best-story list. It uses .NET 10 and does not need a database or API key.

## Running the application

Open HackerNewsApi/HackerNewsApi.slnx in Visual Studio with .NET 10 support and run
the HackerNewsApi project.

Open http://localhost:5062/swagger for manual testing. Expand GET /api/stories/best,
enter a number for 'n', and click "Execute".
Swagger is enabled in Development. Its JSON document is at /openapi/v1.json.

You can also call http://localhost:5062/api/stories/best?n=5 directly.

## How it works

1. StoriesController checks that n is greater than zero and asks the service for stories.
2. BestStoriesService checks its cached list. If it needs updating, it calls HackerNewsClient.
3. HackerNewsClient gets the best-story IDs and fetches each story one at a time.
4. The service sorts all the stories by score, converts them into the response model,
   and keeps the list for one minute. It returns the first n stories.

The service is registered as a singleton so every request uses the same cache.
A SemaphoreSlim allows only one request into the cache code at a time. This prevents
several incoming requests from all downloading the same stories. The finally block
releases the semaphore even if something goes wrong.

Fetching stories one at a time is deliberately simple and limits pressure on Hacker News.
The first request, and the first request after the cache expires, will take longer.
Other requests wait for that fetch to finish. Requests during the following minute
use the cached list without calling Hacker News.

If fetching fails, the API returns 503 and waits 30 seconds before trying again.
This short pause avoids repeatedly calling Hacker News during an outage. Errors are
logged on the server. Old results are not returned after a failed refresh.

The strict JSON number setting in Program.cs keeps Swagger's integer input working
correctly. The explicit Microsoft.OpenApi version retains the security fix already
included in this project.

Upstream documentation: [Hacker News API](https://github.com/HackerNews/API).

## Visual Studio Tests

The tests run the API with fake Hacker News responses, so they do not need the public
service. They cover ranking, response fields, invalid counts, cached requests, 100
simultaneous requests sharing one fetch, unavailable items, empty feeds, failure handling,
and the Swagger integer schema. They do not benchmark production throughput or test
the passage of the full one-minute cache lifetime.

## Possible improvements

With more time, I would consider fetching a small number of stories concurrently to
reduce cold-request latency, adding tests for cache expiry, and adding an overall fetch
timeout. Multiple application instances would need a shared cache to avoid duplicate work.

