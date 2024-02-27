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
    private static readonly Random _random = new Random();
    private static readonly ConcurrentBag<Node> _nodes = new();
    private static readonly ConcurrentDictionary<Guid, (int term, Guid? candidate)> _votes = new();
    private static bool _isRunning = true;
    private readonly System.Timers.Timer _actionTimer;
    private readonly object _logLock = new();
    private int _currentTerm = 0;
    private Guid? _votedFor = null;

    public Role Role { get; set; }
    public Guid Id { get; set; }

    public Node()
    {
        Id = Guid.NewGuid();
        Role = Role.Follower;
        _actionTimer = new System.Timers.Timer(GetElectionTimeout());
        _actionTimer.AutoReset = false;
        _actionTimer.Elapsed += DoAction;
        _nodes.Add(this);
    }

    private int GetElectionTimeout()
    {
        return _random.Next(150, 300);
    }

    private void DoAction(object? sender, ElapsedEventArgs e)
    {
        if (!_isRunning) return;

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

    private void BecomeCandidate()
    {
        Role = Role.Candidate;
        _currentTerm++;
        _votedFor = Id;
        _votes[Id] = (_currentTerm, Id);
        Log($"Becoming Candidate in term {_currentTerm}");

        var votesReceived = 1;

        foreach (var node in _nodes)
        {
            if (node.Id != Id)
            {
                var voteGranted = node.Vote(_currentTerm, Id);
                if (voteGranted) votesReceived++;
            }
        }

        if (votesReceived > _nodes.Count / 2)
        {
            Role = Role.Leader;
            Log("Became Leader");
            SendHeartbeat();
        }

        ResetActionTimer();
    }

    public bool Vote(int candidateTerm, Guid candidateId)
    {
        if ((_votedFor == null || _currentTerm < candidateTerm) && _currentTerm <= candidateTerm)
        {
            _votedFor = candidateId;
            _currentTerm = candidateTerm;
            _votes[Id] = (candidateTerm, candidateId);
            Log($"Voting for {candidateId} in term {candidateTerm}");
            ResetActionTimer();
            return true;
        }
        return false;
    }

    private void SendHeartbeat()
    {
        Log("Sending Heartbeat");
        foreach (var node in _nodes)
        {
            if (node.Id != Id)
            {
                node.ReceiveHeartbeat(_currentTerm);
            }
        }
        ResetActionTimer();
    }

    public void ReceiveHeartbeat(int leaderTerm)
    {
        if (leaderTerm >= _currentTerm)
        {
            _currentTerm = leaderTerm;
            Role = Role.Follower;
            Log($"Received Heartbeat in term {_currentTerm}");
            ResetActionTimer();
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
            if (System.IO.Directory.Exists("logs") == false)
            {
                System.IO.Directory.CreateDirectory("logs");
            }

            if (System.IO.Directory.Exists("logs"))
            {
                var fileName = $"logs/{Id}.log";
                System.IO.File.AppendAllText(fileName, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} [{Role}] {message}\n");
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
