using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        [HttpGet("strong")]
        public int StrongGet(string key)
        {
            return 42;
        }

        [HttpGet("eventual")]
        public int EventualGet(string key)
        {
            return 42;
        }

        [HttpPost("compare-and-swap")]
        public bool CompareAndSwap(string key, int oldValue, int newValue)
        {
            return true;
        }
    }
}
