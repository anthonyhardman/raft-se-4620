using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        private readonly ILogger<StorageController> _logger;

        public StorageController(ILogger<StorageController> logger)
        {
            _logger = logger;
        }

        [HttpGet("strong")]
        public int StrongGet(string key)
        {
            _logger.LogInformation("StrongGet called with key {key}", key);
            return 42;
        }

        [HttpGet("eventual")]
        public int EventualGet(string key)
        {
            _logger.LogInformation("EventualGet called with key {key}", key);
            return 42;
        }

        [HttpPost("compare-and-swap")]
        public bool CompareAndSwap(string key, int oldValue, int newValue)
        {
            _logger.LogInformation("CompareAndSwap called with key {key}, oldValue {oldValue}, newValue {newValue}", key, oldValue, newValue);
            return true;
        }
    }
}
