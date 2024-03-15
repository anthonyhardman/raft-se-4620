namespace Raft.Shared;

public class LogEntry
{
  public LogEntry(int term, string key, int value)
  {
    Term = term;
    Key = key;
    Value = value;
  }

  public int Term { get; }
  public string Key { get; }
  public int Value { get; }
}