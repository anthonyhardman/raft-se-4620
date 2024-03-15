namespace Raft.Shared;

public class AppendEntriesResponse
{
  public bool Success { get; set; }
  public int Term { get; set; }

  public void Deconstruct(out bool success, out int term)
  {
    success = Success;
    term = Term;
  }
}