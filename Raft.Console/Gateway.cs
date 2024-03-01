namespace Raft.Console;



public class Gateway
{
    private readonly Random _random = new();

    private Node? Leader => Node.Nodes.FirstOrDefault(n => n.Role == Role.Leader);

    public int EventualGet(string key)
    {
        var node = Node.Nodes[_random.Next(Node.Nodes.Count)];
        var (value, logIndex) = node.StateMachine[key];

        return 42;
    }

    public int StrongGet(string key)
    {
        var (value, logIndex) = Leader.StateMachine[key];
        return value;
    }

    public bool CompareAndSwap(string key, int oldValue, int newValue)
    {
        var (value, logIndex) = Leader.StateMachine[key];

        if (value == oldValue)
        {
            Leader.StateMachine[key] = (newValue, logIndex + 1);
            return true;
        }

        return false;
    }

    public void Put(string key, int value)
    {
        var logFile = $"logs/{Leader.Id}.log";
        var prevLogIndex = Leader.LastLogIndex;
        var prevLogTerm = prevLogIndex > 0 ? int.Parse(File.ReadLines(logFile).Last().Split(' ')[0]) : 0;
        File.AppendAllText($"logs/{Leader.Id}", $"{Leader.CurrentTerm} {key} {value}\n");

        var entries = new[] { new LogEntry(Leader.CurrentTerm, key, value, prevLogIndex) };

        var successCount = 0;
        foreach (var node in Node.Nodes)
        {
            var (_, success) = node.AppendEntries(Leader.CurrentTerm, Leader.Id, prevLogIndex, prevLogIndex, entries, Leader.CommitIndex);
            if (success) successCount++;
        }

        if (successCount > Node.Nodes.Count / 2)
        {
            Leader.CommitIndex = prevLogIndex;
            Leader.StateMachine[key] = (value, Leader.StateMachine.Count);
        }
    }
}