namespace Raft.Shared;

public class RequestVoteRequest
{
  public int Term { get; set; }
  public string CandidateId { get; set; }
  public int LastLogIndex { get; set; }
  public int LastLogTerm { get; set; }

  public RequestVoteRequest(int term, string candidateId, int lastLogIndex, int lastLogTerm)
  {
    Term = term;
    CandidateId = candidateId;
    LastLogIndex = lastLogIndex;
    LastLogTerm = lastLogTerm;
  }
}

public class RequestVoteResponse
{
  public bool VoteGranted { get; set; }
  public int Term { get; set; }

  public void Deconstruct(out bool voteGranted, out int term)
  {
    voteGranted = VoteGranted;
    term = Term;
  }
}