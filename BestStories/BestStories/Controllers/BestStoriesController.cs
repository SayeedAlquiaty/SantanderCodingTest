using BestStories.Sevices;
using Microsoft.AspNetCore.Mvc;

namespace BestStories.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BestStoriesController : ControllerBase
    {
        private readonly ILogger<BestStoriesController> _logger;
        private readonly BestStoriesCacheService bestStoriesCacheService;

        public BestStoriesController(ILogger<BestStoriesController> logger, BestStoriesCacheService bestStoriesCacheService)
        {
            _logger = logger;

            this.bestStoriesCacheService = bestStoriesCacheService;
        }

        [HttpGet]
        [Route("get/{num}")]
        public IEnumerable<object> Get(int num)
        {
            return bestStoriesCacheService.GetBestStories(num);
        }
    }
}