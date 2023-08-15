using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using BestStories.HackerNews;
using LazyCache;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace BestStories.Sevices
{
    public class StoryInfo
    {
        public string By { get; set; }

        public string Descendants { get; set; }

        public string Id { get; set; }

        public int Score { get; set; }

        public string Time { get; set; }

        public string Title { get; set; }

        public string Type { get; set; }

        public string Url { get; set; }

        public List<string> Kids { get; set; }
    }

    public class BestStoriesCacheService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<BestStoriesCacheService> logger;
        private readonly HackerNewsApiConfiguration hackerNewsApiConfiguration;

        private readonly HttpClient client;
        private readonly IAppCache cache;


        public BestStoriesCacheService(IOptions<HackerNewsApiConfiguration> hackerNewsApiConfiguration, IHttpClientFactory httpClientFactory,
            IAppCache cache, ILogger<BestStoriesCacheService> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
            this.hackerNewsApiConfiguration = hackerNewsApiConfiguration.Value;
            BestStories = new List<StoryInfo>();
            this.cache = cache;
            client = httpClientFactory.CreateClient();
        }

        public List<StoryInfo> BestStories { get; set; }


        public async Task LoadStoriesFromApi()
        {
            var cacheKey = GetHackerNewsStoryIdsCacheKey();
            var cacheDurationMinutes = 10;

            logger.LogInformation("Loading stories from HackerNews");

            var sw = new Stopwatch();
            sw.Start();

            var url = new Uri(hackerNewsApiConfiguration.BestStoriesIdUrl);

            try
            {
                logger.LogInformation($"Calling HackerNews API for List for story numbers");

                var httpResponse = client.GetAsync(url).Result;
                var response = httpResponse.Content.ReadAsStringAsync().Result;

                if (httpResponse.IsSuccessStatusCode)
                {
                    var stories = JsonConvert.DeserializeObject<List<string>>(response);

                    using (BlockingCollection<StoryInfo> bestStories = new BlockingCollection<StoryInfo>())
                    {
                        Parallel.ForEach(stories, story =>
                        {
                            var urls = new Uri(hackerNewsApiConfiguration.StoryDetailsUrl + story + ".json");
                            var httpResponse1 = client.GetAsync(urls).Result;
                            var storyDetails = httpResponse1.Content.ReadAsStringAsync().Result;
                            var details = JsonConvert.DeserializeObject<StoryInfo>(storyDetails);

                            bestStories.Add(details);
                        });
                        bestStories.CompleteAdding();

                        BestStories.AddRange(bestStories);
                        cache.Add(cacheKey, BestStories, TimeSpan.FromMinutes(cacheDurationMinutes));
                    }
                }
                else
                {
                    if (httpResponse.StatusCode.Equals(HttpStatusCode.NotFound))
                    {
                        logger.LogInformation($"Stories was not found");
                    }

                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $@"Error calling  HackerNews API => {hackerNewsApiConfiguration.BestStoriesIdUrl}");
            }
            finally
            {
                logger.LogInformation($"End Calling  HackerNews API => {hackerNewsApiConfiguration.BestStoriesIdUrl}");
                cache.Add(cacheKey, BestStories, TimeSpan.FromMinutes(cacheDurationMinutes));
            }
        }

        public IEnumerable<object> GetBestStories(int count)
        {

            var cacheKey = GetHackerNewsStoryIdsCacheKey();
            BestStories = cache.Get<List<StoryInfo>>(cacheKey);

            if (BestStories == null)
                return new List<string> { "No stories in the store yet! Please try refresh in few seconds time thanks you!" };

            if (count > BestStories.Count)
            {
                return BestStories.OrderByDescending(st => st.Score).Select(s => new { s.Title, Uri = s.Url, PostedBy = s.By, s.Time, s.Score, CommentCount = s.Descendants });
            }

            return BestStories.OrderByDescending(st => st.Score).Take(count).Select(s => new { s.Title, Uri = s.Url, PostedBy = s.By, s.Time, s.Score, CommentCount = s.Descendants });
        }


        private static string GetHackerNewsStoryIdsCacheKey()
        {
            return $"HackerNewsStoryIds";
        }

        private static string GetHackerNewsStoryDetailsCacheKey()
        {
            return $"HackerNewsStoryDetails";
        }
    }
}
