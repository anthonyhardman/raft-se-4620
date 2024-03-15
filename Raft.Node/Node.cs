using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;
using Raft.Shared;

namespace Raft.Node;

public enum Role
{
    Follower,
    Candidate,
    Leader
}

public class Node
{
    private static readonly Random _random = new();
    public static int MinResetTimeout { get; set; } = 750;
    public static int MaxResetTimeout { get; set; } = 1000;
    public static List<string> Nodes { get; set; } = [];

    public Role Role { get; set; }
    public string Id { get; set; }
    private readonly System.Timers.Timer _actionTimer;
    public string LogFile => $"logs/raft-node.log";
    public int LastLogIndex => File.ReadLines(LogFile).Count() - 1;
    public int LastLogTerm => LastLogIndex >= 0 ? GetLogEntry(LastLogIndex).Term : 0;
    public int CommitIndex { get; set; } = -1;
    public int LastApplied { get; set; } = -1;
    public int CurrentTerm { get; set; } = 0;
    public bool Healthy { get; set; } = true;
    public string? VotedFor { get; set; } = null;
    public string? MostRecentLeader { get; set; } = null;
    public Dictionary<string, (int value, int logIndex)> StateMachine { get; set; } = new();


    private void CreateLogFile()
    {
        if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");
        if (!File.Exists(LogFile)) File.Create(LogFile).Close();
    }


    public Node(string id, List<string> nodes)
    {
        Id = id;
        Role = Role.Follower;
        Nodes = nodes;
        Nodes.Add(id);
        CreateLogFile();
        _actionTimer = new System.Timers.Timer(GetElectionTimeout());
        _actionTimer.AutoReset = false;
        _actionTimer.Elapsed += DoAction;
        _actionTimer.Start();
        Console.WriteLine($"Node {Id} started");
    }

    public void AppendEntry(int term, string key, int value)
    {
        File.AppendAllText(LogFile, $"{term} {key} {value}\n");
        Console.WriteLine($"Node {Id} appended entry {key} with value {value}");
    }

    public LogEntry GetLogEntry(int index)
    {
        var line = File.ReadLines(LogFile).ElementAt(index);
        var parts = line.Split(' ');
        Console.WriteLine($"Node {Id} got log entry {index}");
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

        Console.WriteLine($"Node {Id} got log entries from {prevLogIndex}");
        return [.. entries];
    }

    public (bool success, int term) AppendEntries(int term, string leaderId, int prevLogIndex, int prevLogTerm, LogEntry[] entries)
    {
        Console.WriteLine($"{JsonSerializer.Serialize(StateMachine)}");
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
            Console.WriteLine($"Node {Id} rejected entries from {leaderId} due to log inconsistency");
            return (false, CurrentTerm);
        }

        CurrentTerm = term;
        Role = Role.Follower;
        VotedFor = null;
        MostRecentLeader = leaderId;

        int nextIndexToAppend = prevLogIndex + 1;
        foreach (var entry in entries)
        {
            if (LastLogIndex < nextIndexToAppend)
            {
                AppendEntry(entry.Term, entry.Key, entry.Value);
            }
            nextIndexToAppend++;
        }

        CommitIndex = LastLogIndex;

        UpdateStateMachine();

        ResetActionTimer();
        Console.WriteLine($"Node {Id} appended new entries from {leaderId}");
        return (true, CurrentTerm);
    }

    public async Task<bool> SendHeartbeat(int prevLogIndex)
    {
        var prevLogTerm = prevLogIndex > 0 ? GetLogEntry(prevLogIndex).Term : 0;
        var successCount = 0;

        foreach (var key_value in StateMachine)
        {
            Console.WriteLine($"key: {key_value.Key}, value: {key_value.Value}");
        }

        foreach (var node in Nodes)
        {
            if (node == Id) continue;
            var success = false;
            var term = 0;
            while (!success)
            {
                var entries = GetLogEntries(prevLogIndex);

                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri($"http://{node}");
                var response = httpClient.PostAsJsonAsync("api/node/append-entries", new AppendEntriesRequest(CurrentTerm, Id, prevLogIndex, prevLogTerm, entries)).Result;
                (success, term) = await response.Content.ReadFromJsonAsync<AppendEntriesResponse>();

                if (term > CurrentTerm)
                {
                    CurrentTerm = term;
                    Role = Role.Follower;
                    ResetActionTimer();
                    Console.WriteLine($"Node {Id} became follower");
                    return false;
                }

                if (success)
                {
                    successCount++;
                }
                else
                {
                    prevLogIndex--;
                    prevLogTerm = prevLogIndex < 0 ? 0 : GetLogEntry(prevLogIndex).Term;
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
            Console.WriteLine($"Node {Id} updated state machine");
        }

        ResetActionTimer();
        Console.WriteLine($"Node {Id} sent heartbeat");
        return true;
    }

    public void UpdateStateMachine()
    {
        while (LastApplied < CommitIndex)
        {
            LastApplied++;
            var entry = GetLogEntry(LastApplied);
            StateMachine[entry.Key] = (entry.Value, LastApplied);
        }
        Console.WriteLine($"Node {Id} updated state machine");
    }

    public (int term, bool voteGranted) RequestVote(int term, string candidateId, int lastLogIndex, int lastLogTerm)
    {
        if (!Healthy)
        {
            Console.WriteLine($"Node {Id} rejected vote request from {candidateId} because it is not healthy");
            return (CurrentTerm, false);
        }

        if (term <= CurrentTerm)
        {
            Console.WriteLine($"Node {Id} rejected vote request from {candidateId} because term {term} is less than {CurrentTerm}");
            return (CurrentTerm, false);
        }

        if (VotedFor == null || VotedFor == candidateId)
        {
            if (lastLogIndex >= LastLogIndex && lastLogTerm >= LastLogTerm)
            {
                VotedFor = candidateId;
                Console.WriteLine($"Node {Id} voted for {candidateId}");
                return (CurrentTerm, true);
            }
        }

        ResetActionTimer();
        Console.WriteLine($"Node {Id} rejected vote request from {candidateId}");
        return (CurrentTerm, false);
    }

    private async Task BecomeCandidate()
    {
        Role = Role.Candidate;
        CurrentTerm++;
        VotedFor = Id;

        var votes = 1;

        Console.WriteLine($"{Id} started election for term {CurrentTerm} with nodes {Nodes.Count}");
        foreach (var node in Nodes)
        {
            if (node == Id) continue;
            Console.WriteLine($"Node {Id} requesting vote from {node}");

            try
            {
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri($"http://{node}");
                Console.WriteLine($"Node {Id} requesting vote from {node} at {httpClient.BaseAddress}");
                var response = await httpClient.PostAsJsonAsync("api/node/request-vote", new RequestVoteRequest(CurrentTerm, Id, LastLogIndex, LastLogTerm));

                var (voteGranted, term) = await response.Content.ReadFromJsonAsync<RequestVoteResponse>();

                if (term > CurrentTerm)
                {
                    CurrentTerm = term;
                    Role = Role.Follower;
                    Console.WriteLine($"Node {Id} became follower");
                    return;
                }

                if (voteGranted)
                {
                    Console.WriteLine($"Node {Id} received vote from {node}");
                    votes++;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Node {Id} failed to request vote from {node}");
            }
        }

        if (votes > Nodes.Count / 2)
        {
            Role = Role.Leader;
            MostRecentLeader = Id;
            await SendHeartbeat(LastLogIndex);
            Console.WriteLine($"Node {Id} became leader");
        }
        Console.WriteLine($"Node {Id} became candidate");
        ResetActionTimer();
    }

    public async void DoAction(object? sender, ElapsedEventArgs e)
    {
        Console.WriteLine($"Node {Id} doing action");
        if (!Healthy)
        {
            return;
        }

        switch (Role)
        {
            case Role.Follower:
            case Role.Candidate:
                await BecomeCandidate();
                break;
            case Role.Leader:
                await SendHeartbeat(LastLogIndex);
                break;
        }
    }

    private void ResetActionTimer()
    {
        _actionTimer.Stop();
        _actionTimer.Interval = GetElectionTimeout();
        _actionTimer.Start();
    }

    private int GetElectionTimeout()
    {
        if (Role == Role.Leader)
        {
            return 25;
        }
        return _random.Next(MinResetTimeout, MaxResetTimeout);
    }

    public int GetValue(string key)
    {
        if (!StateMachine.ContainsKey(key))
        {
            throw new Exception($"Key {key} not found, {Id}");
        }

        var (value, _) = StateMachine[key];

        return value;
    }

    public async Task<int> StrongGet(string key)
    {
        if (!(await LeaderConsensus()))
        {
            throw new Exception($"Leader consensus failed, {Id}");
        }

        return GetValue(key);
    }

    public async Task<bool> LeaderConsensus()
    {
        Console.WriteLine($"Node {Id} checking leader consensus");
        if (Role != Role.Leader)
        {
            return false;
        }

        var agree = 1;
        foreach (var node in Nodes)
        {
            if (node == Id) continue;
            var httpClient = new HttpClient();
            Console.WriteLine($"Node {Id} checking leader consensus with {node}");
            httpClient.BaseAddress = new Uri($"http://{node}");
            var response = await httpClient.GetAsync("/api/node/leader");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Node {Id} leader consensus failed with {node}");
                continue;
            }

            var leader = await response.Content.ReadAsStringAsync();

            if (leader == Id)
            {
                agree++;
            }

            if (agree > Nodes.Count / 2)
            {
                return true;
            }
        }

        return true;
    }
}
