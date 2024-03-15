namespace Raft.Shared;

public class AppendRequest
{
  public string Key { get; set; }
  public int Value { get; set; }

  public AppendRequest(string key, int value)
  {
    Key = key;
    Value = value;
  }
}
