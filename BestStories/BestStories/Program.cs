namespace BestStories
{
    using BestStories.HackerNews;
    using BestStories.Sevices;
    using Microsoft.Extensions.Configuration;

    //using LazyCache;
    //using LazyCache.Providers;
    //using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ConfigurationManager configuration = builder.Configuration;

            // Add services to the container.
            builder.Services.AddHostedService<BestStoriesCacheRefreshHostedService>();
            builder.Services.AddScoped<BestStoriesCacheService>();

            builder.Services.AddHttpClient("BestStories");
            builder.Services.AddLazyCache();
            builder.Services.AddControllers();
            builder.Services.Configure<CachingOptions>(configuration.GetSection("Caching"));

            builder.Services.Configure<HackerNewsApiConfiguration>(configuration.GetSection("HackerNewsApiConfiguration"));

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}