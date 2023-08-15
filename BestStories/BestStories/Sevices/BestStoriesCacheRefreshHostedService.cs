using Microsoft.Extensions.Options;

namespace BestStories.Sevices
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BestStories;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Polly;

    public class BestStoriesCacheRefreshHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<BestStoriesCacheRefreshHostedService> logger;

        private readonly IServiceScopeFactory serviceScopeFactory;

        private Timer communityToRefreshTimer;

        private Timer cacheRefreshTimer;

        private Timer dailyCacheRefreshTimer;

        private bool disposed;

        private readonly CachingOptions options;

        public BestStoriesCacheRefreshHostedService(ILogger<BestStoriesCacheRefreshHostedService> logger,
                                                    IServiceScopeFactory serviceScopeFactory,
                                                    IOptions<CachingOptions> cachingOptions)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            options = cachingOptions.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Timed Hosted Service running.");

            InitCacheRefreshTimer();

            InitFullCacheRefreshTimer();

            return Task.CompletedTask;
        }

        private void InitFullCacheRefreshTimer()
        {
            const int DefaultPeriodMinutes = 720;
            var period = options.ApiCallFrequencyMinutes <= 0 ? DefaultPeriodMinutes : options.ApiCallFrequencyMinutes;
            period -= 5;
            logger.LogInformation($"Timed Hosted Service - Full cache refresh timer will run every {period} minutes");
            dailyCacheRefreshTimer = new Timer(async o => await FullCacheRefresh(o), null, TimeSpan.Zero, TimeSpan.FromMinutes(period));
        }

        private async Task FullCacheRefresh(object state)
        {
            var retryCount = 3;
            await Policy.Handle<Exception>()
                .RetryAsync(retryCount,
                    (exception, i) =>
                    {
                        logger.LogWarning($"Timed Hosted Service - Retry init cache failed. Retry attempt {i}");
                    })
                .ExecuteAsync(async () =>
                {
                    logger.LogInformation("Timed Hosted Service - Start cache refresh");
                    using (var scope = serviceScopeFactory.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetService<BestStoriesCacheService>();
                        await service.LoadStoriesFromApi().ConfigureAwait(false);
                    }
                    logger.LogInformation("Timed Hosted Service - Finish cache refresh for all communities");
                });
        }

        private void InitCacheRefreshTimer()
        {
            const int DefaultPeriodSeconds = 180;
            var period = options.RefreshIntervalSeconds <= 0 ? DefaultPeriodSeconds : options.RefreshIntervalSeconds;

            logger.LogInformation($"Periodic cache refresh will run every {period} seconds");

            async void Callback(object state)
            {
                await TriggerRefresh(state).ConfigureAwait(false);
            }

            cacheRefreshTimer = new Timer(Callback, null, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(period));
        }

        private async Task TriggerRefresh(object state)
        {
            logger.LogDebug("Timed Hosted Service - start refreshing cache.");
            using (var scope = serviceScopeFactory.CreateScope())
            {
                await RefreshCache(scope.ServiceProvider).ConfigureAwait(false);
            }
            logger.LogDebug("Timed Hosted Service - finished refreshing cache.");
        }

        private async Task RefreshCache(IServiceProvider provider)
        {
            var service = provider.GetService<BestStoriesCacheService>();
            try
            {
                await service.LoadStoriesFromApi().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Timed Hosted Service - Failed to refresh cache for community");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                cacheRefreshTimer?.Dispose();
                dailyCacheRefreshTimer?.Dispose();
                communityToRefreshTimer?.Dispose();
            }

            disposed = true;
        }

        ~BestStoriesCacheRefreshHostedService()
        {
            Dispose(false);
        }
    }
}
