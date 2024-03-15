using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raft.Node;
using Raft.Shared;

namespace MyApp.Namespace;

[Route("api/[controller]")]
[ApiController]
public class NodeController : ControllerBase
{
  private readonly Node _node;

  public NodeController(Node node)
  {
    _node = node;
  }

  [HttpPost("append-entries")]
  public ActionResult<AppendEntriesResponse> Append(AppendEntriesRequest request)
  {
    var (success, term) = _node.AppendEntries(request.Term, request.LeaderId, request.PrevLogIndex, request.PrevLogTerm, request.Entries);

    return Ok(new {
      success,
      term
    });
  }

  [HttpPost("request-vote")]
  public ActionResult<RequestVoteResponse> RequestVote(RequestVoteRequest request)
  {
    var (term, voteGranted) = _node.RequestVote(request.Term, request.CandidateId, request.LastLogIndex, request.LastLogTerm);

    return Ok(new {
      voteGranted,
      term
    });
  }

  [HttpGet("value")]
  public ActionResult<int> GetValue(string key)
  {
    if (!_node.StateMachine.ContainsKey(key))
    {
      return NotFound($"Key {key} not found, {_node.Id}");
    }

    var (value, _) = _node.StateMachine[key];

    return Ok(value);
  }

  [HttpPost("append")]
  public ActionResult AppendEntry(AppendRequest request)
  {
    _node.AppendEntry(_node.CurrentTerm, request.Key, request.Value);
    // _node.UpdateStateMachine();
    return Ok();
  }

  [HttpGet("role")]
  public ActionResult<string> GetRole()
  {
    return Ok(_node.Role.ToString());
  }
}
