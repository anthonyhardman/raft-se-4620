using System;
using System.IO;
using System.Collections.Concurrent;
using System.Timers;

namespace Raft.Console;

public enum Role
{
    Follower,
    Candidate,
    Leader
}

public class Node
{
    private static readonly Random _random = new();
    private static bool _isRunning = true;
    public static int MinResetTimeout { get; set; } = 150;
    public static int MaxResetTimeout { get; set; } = 300;
    public static List<Node> Nodes { get; set; } = [];

    public Role Role { get; set; }
    public Guid Id { get; set; }
    private readonly System.Timers.Timer _actionTimer;
    public string LogFile => $"logs/{Id}.log";
    public int LastLogIndex => File.ReadLines(LogFile).Count() - 1;
    public int CommitIndex { get; set; } = -1;
    public int LastApplied { get; set; } = -1;
    public int CurrentTerm { get; set; } = 0;
    public bool Healthy { get; set; } = true;
    public ConcurrentDictionary<string, (int value, int logIndex)> StateMachine { get; set; } = new();

    private void CreateLogFile()
    {
        if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");
        if (!File.Exists(LogFile)) File.Create(LogFile).Close();
    }


    public Node()
    {
        Id = Guid.NewGuid();
        Role = Role.Follower;
        Nodes.Add(this);
        CreateLogFile();
    }

    public void AppendEntry(int term, string key, int value)
    {
        File.AppendAllText(LogFile, $"{term} {key} {value}\n");
    }

    public LogEntry GetLogEntry(int index)
    {
        var line = File.ReadLines(LogFile).ElementAt(index);
        var parts = line.Split(' ');
        return new LogEntry(int.Parse(parts[0]), parts[1], int.Parse(parts[2]));
    }

    public LogEntry[] GetLogEntries(int prevLogIndex)
    {
        var lines = File.ReadLines(LogFile).Skip(prevLogIndex + 1);
        var entries = new List<LogEntry>();

        foreach (var line in lines)
        {
            var parts = line.Split(' ');
            entries.Add(new LogEntry(int.Parse(parts[0]), parts[1], int.Parse(parts[2])));
        }

        return [.. entries];
    }

    public (bool success, int term) AppendEntries(int term, Guid leaderId, int prevLogIndex, int prevLogTerm, LogEntry[] entries)
    {
        if (!Healthy)
        {
            return (false, CurrentTerm);
        }
        
        if (term < CurrentTerm)
        {
            return (false, CurrentTerm);
        }

        if (LastLogIndex < prevLogIndex || (prevLogIndex >= 0 && GetLogEntry(prevLogIndex).Term != prevLogTerm))
        {
            return (false, CurrentTerm);
        }

        CurrentTerm = term;
        foreach (var entry in entries)
        {
            AppendEntry(entry.Term, entry.Key, entry.Value);
        }
        
        CommitIndex = LastLogIndex;

        UpdateStateMachine();

        return (true, CurrentTerm);
    }

    public bool SendHeartbeat(int prevLogIndex)
    {
        var prevLogTerm = prevLogIndex > 0 ? GetLogEntry(prevLogIndex).Term : 0;
        var successCount = 0;

        foreach (var node in Nodes)
        {
            if (node.Id == Id) continue;
            var success = false;
            var term = 0;
            while (!success)
            {
                var entries = GetLogEntries(prevLogIndex);
                (success, term) = node.AppendEntries(CurrentTerm, Id, prevLogIndex, prevLogTerm, entries);

                if (term > CurrentTerm)
                {
                    CurrentTerm = term;
                    Role = Role.Follower;
                    return false;
                }

                if (success)
                {
                    successCount++;
                }
                else
                {
                    prevLogIndex--;
                    prevLogTerm = prevLogIndex < 0 ? 0 :  GetLogEntry(prevLogIndex).Term;
                }

                if (prevLogIndex < -1)
                {
                    break;
                }
            }

        }

        if (successCount >= Nodes.Count / 2)
        {
            CommitIndex = LastLogIndex;

            UpdateStateMachine();
        }

        return true;
    }

    private void UpdateStateMachine()
    {
        while (LastApplied < CommitIndex)
        {
            LastApplied++;
            var entry = GetLogEntry(LastApplied);
            StateMachine[entry.Key] = (entry.Value, LastApplied);
        }
    }

    // public Node()
    // {

    // }

    // public (int term, bool voteGranted) RequestVote(int term, Guid candidateId, int lastLogIndex, int lastLogTerm)
    // {

    // }

    // public void SendHeartbeat()
    // {

    // }

    // public (int term, bool success) AppendEntries(int term, Guid leaderId, int prevLogIndex, int prevLogTerm, LogEntry[] entries, int leaderCommit)
    // {

    // }

    // public void DoAction(object? sender, ElapsedEventArgs e)
    // {

    // }

    // private void BecomeCandidate()
    // {

    // }

    // private int GetElectionTimeout()
    // {
    //     if (Role == Role.Leader)
    //     {
    //         return 175;
    //     }
    //     return _random.Next(MinResetTimeout, MaxResetTimeout);
    // }

    // private void ResetActionTimer()
    // {
    //     _actionTimer.Stop();
    //     _actionTimer.Interval = GetElectionTimeout();
    //     _actionTimer.Start();
    // }

    // public void Run()
    // {
    //     _actionTimer.Start();

    //     while (_isRunning)
    //     {
    //         Thread.Sleep(1000);
    //     }

    //     _actionTimer.Stop();
    // }

    // public static void StopAll()
    // {
    //     _isRunning = false;
    // }
}
