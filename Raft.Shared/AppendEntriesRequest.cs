namespace Raft.Shared;

public class AppendEntriesRequest
{
  public int Term { get; set; }
  public string LeaderId { get; set; }
  public int PrevLogIndex { get; set; }
  public int PrevLogTerm { get; set; }
  public LogEntry[] Entries { get; set; }

  public AppendEntriesRequest(int term, string leaderId, int prevLogIndex, int prevLogTerm, LogEntry[] entries)
  {
    Term = term;
    LeaderId = leaderId;
    PrevLogIndex = prevLogIndex;
    PrevLogTerm = prevLogTerm;
    Entries = entries;
  }
}
