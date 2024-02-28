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
    public static ConcurrentBag<Node> Nodes { get; set; } = [];
    private static readonly ConcurrentDictionary<Guid, (int term, Guid? candidate)> _votes = new();
    public static int MinResetTimeout { get; set; } = 150;
    public static int MaxResetTimeout { get; set; } = 300;
    private static bool _isRunning = true;
    private readonly System.Timers.Timer _actionTimer;
    private readonly object _logLock = new();
    public int CurrentTerm { get; set; } = 0;
    private Guid? _votedFor = null;

    public Role Role { get; set; }
    public Guid Id { get; set; }
    public bool Healthy { get; set; } = true;
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
    }

    public static void Reset()
    {
        Nodes.Clear();
        _votes.Clear();
    }


    private static int GetElectionTimeout()
    {
        return _random.Next(MinResetTimeout, MaxResetTimeout);
    }

    public void DoAction(object? sender, ElapsedEventArgs e)
    {
        if (!_isRunning) return;

        if (Healthy)
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
                    SendHeartbeat();
                    break;
            }
        }
    }

    private void BecomeCandidate()
    {
        Role = Role.Candidate;
        CurrentTerm++;
        // _votedFor = Id;
        _votes[Id] = (CurrentTerm, Id);
        Log($"Becoming Candidate in term {CurrentTerm}");

        var votesReceived = 1;

        foreach (var node in Nodes)
        {
            if (node.Id != Id)
            {
                var voteGranted = node.Vote(CurrentTerm, Id);
                if (voteGranted) votesReceived++;
            }
        }

        if (votesReceived > Nodes.Count / 2)
        {
            Role = Role.Leader;
            Log("Became Leader");
            SendHeartbeat();
        }

        ResetActionTimer();
    }

    public bool Vote(int candidateTerm, Guid candidateId)
    {
        if (Healthy)
        {
            var lastVote = _votes.GetValueOrDefault(Id, (term: 0, candidate: null));
            if ((lastVote.candidate == null || lastVote.term < candidateTerm) && lastVote.term <= candidateTerm)
            {
                // _votedFor = candidateId;
                CurrentTerm = candidateTerm;
                _votes[Id] = (candidateTerm, candidateId);
                Log($"Voting for {candidateId} in term {candidateTerm}");
                ResetActionTimer();
                return true;
            }
        }
        return false;
    }

    private void SendHeartbeat()
    {
        if (Healthy)
        {
            Log("Sending Heartbeat");
            foreach (var node in Nodes)
            {
                if (node.Id != Id)
                {
                    node.ReceiveHeartbeat(CurrentTerm);
                }
            }
            ResetActionTimer();
        }
    }

    public void ReceiveHeartbeat(int leaderTerm)
    {
        if (Healthy)
        {
            if (leaderTerm >= CurrentTerm)
            {
                CurrentTerm = leaderTerm;
                Role = Role.Follower;
                Log($"Received Heartbeat in term {CurrentTerm}");
                ResetActionTimer();
            }
        }
    }

    private void ResetActionTimer()
    {
        _actionTimer.Stop();
        _actionTimer.Interval = GetElectionTimeout();
        _actionTimer.Start();
    }

    private void Log(string message)
    {
        lock (_logLock)
        {
            if (Directory.Exists("logs") == false)
            {
                Directory.CreateDirectory("logs");
            }

            if (Directory.Exists("logs"))
            {
                var fileName = $"logs/{Id}.log";
                File.AppendAllText(fileName, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} [{Role}] {message}\n");
            }
        }
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
