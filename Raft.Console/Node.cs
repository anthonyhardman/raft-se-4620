using System;
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
    public Guid MostRecentLeader { get; set; }
    public int CurrentTerm { get; set; } = 0;
    public Guid? VotedFor { get; set; } = null;
    public int CommitIndex { get; set; }
    public int LastApplied { get; set; }
    public ConcurrentDictionary<Guid, int> NextIndex { get; set; }
    public ConcurrentDictionary<Guid, int> MatchIndex { get; set; }
    private readonly System.Timers.Timer _actionTimer;
    public ConcurrentDictionary<string, (int value, int logIndex)> StateMachine { get; set; } = new();
    public string LogFile => $"logs/{Id}.log";
    public int LastLogIndex => File.ReadAllLines(LogFile).Length;
    public int LastLogTerm => LastLogIndex > 0 ? int.Parse(File.ReadLines(LogFile).Last().Split(' ')[0]) : 0;
    public bool Healthy { get; set; } = true;

    private int GetElectionTimeout()
    {
        if (Role == Role.Leader)
        {
            return 175;
        }
        return _random.Next(MinResetTimeout, MaxResetTimeout);
    }

    private void CreateLogFile()
    {
        if (Directory.Exists("logs") == false)
        {
            Directory.CreateDirectory("logs");
        }

        if (Directory.Exists("logs") && !File.Exists(LogFile))
        {
            File.Create(LogFile).Close();
        }
    }

    public Node()
    {
        Id = Guid.NewGuid();
        Role = Role.Follower;
        _actionTimer = new System.Timers.Timer(GetElectionTimeout())
        {
            AutoReset = false
        };
        _actionTimer.Elapsed += DoAction;
        Nodes.Add(this);
        CreateLogFile();
    }

    public (int term, bool voteGranted) RequestVote(int term, Guid candidateId, int lastLogIndex, int lastLogTerm)
    {
        if (term < CurrentTerm)
        {
            return (CurrentTerm, false);
        }

        bool isLogUpToDate = lastLogTerm > CurrentTerm || (lastLogTerm == CurrentTerm && lastLogIndex >= CommitIndex);

        if ((VotedFor == null || VotedFor == candidateId) && isLogUpToDate)
        {
            CurrentTerm = term;
            VotedFor = candidateId;
            ResetActionTimer();
            return (CurrentTerm, true);
        }

        ResetActionTimer();
        return (CurrentTerm, false);
    }

    public void SendHeartbeat()
    {
        System.Console.WriteLine($"Node {Id} sending Heartbeat");
        foreach (var node in Nodes)
        {
            if (node.Id != Id)
            {
                node.AppendEntries(CurrentTerm, Id, LastLogIndex, LastLogTerm, [], CommitIndex);
            }
        }
        ResetActionTimer();
    }

    public (int term, bool success) AppendEntries(int term, Guid leaderId, int prevLogIndex, int prevLogTerm, LogEntry[] entries, int leaderCommit)
    {
        if (term < CurrentTerm)
        {
            return (CurrentTerm, false);
        }

        if (term > CurrentTerm)
        {
            MostRecentLeader = leaderId;
            CurrentTerm = term;
            Role = Role.Follower;
            VotedFor = null;
        }

        var logs = File.ReadAllLines(LogFile);
        if (prevLogIndex >= 0 && logs.Length > prevLogIndex && logs[prevLogIndex] != prevLogTerm.ToString())
        {
            return (CurrentTerm, false);
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (logs.Length > prevLogIndex + 1 + i && logs[prevLogIndex + 1 + i] != entries[i].Term.ToString())
            {
                logs = logs.Take(prevLogIndex + 1 + i).ToArray();
            }

            if (logs.Length <= prevLogIndex + 1 + i)
            {
                var newLogs = new List<string>(logs);
                newLogs.Add(entries[i].Term.ToString());
                logs = newLogs.ToArray();
            }
        }

        if (leaderCommit > CommitIndex)
        {
            CommitIndex = Math.Min(leaderCommit, prevLogIndex + entries.Length);
        }

        File.WriteAllLines(LogFile, logs);

        ResetActionTimer();
        return (CurrentTerm, true);
    }

    public void DoAction(object? sender, ElapsedEventArgs e)
    {
        switch (Role)
        {
            case Role.Follower:
                BecomeCandidate();
                break;
            case Role.Candidate:
                BecomeCandidate();
                break;
            case Role.Leader:
                System.Console.WriteLine($"Node {Id} sending Heartbeat");
                SendHeartbeat();
                break;
        }
    }

    private void BecomeCandidate()
    {
        Role = Role.Candidate;
        CurrentTerm++;
        VotedFor = Id;

        var votesReceived = 1;

        foreach (var node in Nodes)
        {
            if (node.Id != Id)
            {
                var (term, voteGranted) = node.RequestVote(CurrentTerm, Id, LastApplied, CurrentTerm);
                if (term > CurrentTerm)
                {
                    Role = Role.Follower;
                    break;
                }
                if (voteGranted) votesReceived++;
            }
        }

        if (votesReceived > Nodes.Count / 2)
        {
            Role = Role.Leader;
            System.Console.WriteLine($"Node {Id} became Leader");
            SendHeartbeat();
        }
        ResetActionTimer();
    }

    private void ResetActionTimer()
    {
        _actionTimer.Stop();
        _actionTimer.Interval = GetElectionTimeout();
        _actionTimer.Start();
    }

    public void Run()
    {
        _actionTimer.Start();

        while (_isRunning)
        {
            Thread.Sleep(1000);
        }

        _actionTimer.Stop();
    }

    public static void StopAll()
    {
        _isRunning = false;
    }
}
