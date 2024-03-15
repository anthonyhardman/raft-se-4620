using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raft.Shared;

namespace MyApp.Namespace
{
  [Route("api/[controller]")]
  [ApiController]
  public class StorageController : ControllerBase
  {
    private readonly ILogger<StorageController> _logger;
    private readonly Random _random = new();
    private List<string> _nodes = new();

    public StorageController(ILogger<StorageController> logger)
    {
      _logger = logger;
      var nodes = Environment.GetEnvironmentVariable("NODES");
      if (nodes != null)
      {
        _nodes = nodes.Split(',').ToList();
      }
    }

    [HttpGet("strong")]
    public async Task<ActionResult<int>> StrongGet(string key)
    {
      _logger.LogInformation("StrongGet called with key {key}", key);

      var leader = await GetLeader();
      Console.WriteLine($"Leader is {leader}---------------------------------");

      if (leader != null)
      {
        var httpClient = new HttpClient
        {
          BaseAddress = new Uri($"http://{leader}")
        };
        Console.WriteLine($"Getting from {leader}");
        var response = await httpClient.GetAsync($"/api/node/StrongGet?key={key}");
        var responseString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
          Console.WriteLine($"Failed to get from {leader}, status code {response.StatusCode}");
          Console.WriteLine(responseString);
          return StatusCode((int)response.StatusCode, responseString);
        }
        var value = int.Parse(responseString);
        return value;
      }

      return 42;
    }

    [HttpGet("eventual")]
    public async Task<ActionResult<int>> EventualGet(string key)
    {
      _logger.LogInformation("EventualGet called with key {key}", key);
      var node = _nodes[_random.Next(_nodes.Count)];
      Console.WriteLine($"Getting from {node}");
      var httpClient = new HttpClient
      {
        BaseAddress = new Uri($"http://{node}")
      };
      var response = await httpClient.GetAsync($"/api/node/value?key={key}");
      var responseString = await response.Content.ReadAsStringAsync();
      if (!response.IsSuccessStatusCode)
      {
        return StatusCode((int)response.StatusCode, responseString);
      }
      var value = int.Parse(responseString);

      return value;
    }

    [HttpPost("compare-and-swap")]
    public bool CompareAndSwap(string key, int oldValue, int newValue)
    {
      _logger.LogInformation("CompareAndSwap called with key {key}, oldValue {oldValue}, newValue {newValue}", key, oldValue, newValue);
      return true;
    }

    [HttpPost("append")]
    public async Task<IActionResult> Append(string key, int value)
    {
      _logger.LogInformation("Append called with key {key}, value {value}", key, value);
      var leader = await GetLeader();
      if (leader != null)
      {
        var httpClient = new HttpClient
        {
          BaseAddress = new Uri($"http://{leader}")
        };
        Console.WriteLine($"Appending to {leader}");
        await httpClient.PostAsJsonAsync("api/node/append", new AppendRequest(key, value));
        return Ok();
      }
      return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private string GetRandomNode()
    {
      return _nodes[_random.Next(_nodes.Count)];
    }

    private async Task<string> GetLeader()
    {
      foreach (var node in _nodes)
      {
        var httpClient = new HttpClient
        {
          BaseAddress = new Uri($"http://{node}")
        };
        var response = await httpClient.GetAsync("/api/node/role");
        var role = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Node {node} is {role}");
        if (role == "Leader")
        {
          return node;
        }
      }
      return null;
    }
  }
}
