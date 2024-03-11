using Raft.Console;
using FluentAssertions;

namespace Raft.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
        Node.MinResetTimeout = int.MaxValue - 1;
        Node.MaxResetTimeout = int.MaxValue;
    }

    [TearDown]
    public void TearDown()
    {
        Node.Nodes.Clear();

        if (Directory.Exists("logs"))
            Directory.Delete("logs", true);
    }

    [Test]
    public void LogFileIsCreated()
    {
        var node = new Node();
        File.Exists(node.LogFile).Should().BeTrue();
    }

    [Test]
    public void CorrectLogIndexIsReturned()
    {
        var node = new Node();
        node.LastLogIndex.Should().Be(-1);
        node.AppendEntry(1, "key", 42);
        node.LastLogIndex.Should().Be(0);
    }

    [Test]
    public void LoggedEntryIsAppended()
    {
        var node = new Node();
        node.AppendEntry(1, "key", 42);
        File.ReadLines(node.LogFile).Last().Should().Be("1 key 42");
    }

    [Test]
    public void GatewayCantPutIfNoLeader()
    {
        var gateway = new Gateway();
        gateway.Put("key", 42).Should().BeFalse();
    }

    [Test]
    public void GatewayCanPutIfLeaderExists()
    {
        var node = new Node();
        node.Role = Role.Leader;
        var gateway = new Gateway();
        gateway.Put("key", 42).Should().BeTrue();
    }

    [Test]
    public void FollowerNodeUpdatesLogsToMatchLeader()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        var gateway = new Gateway();
        gateway.Put("key", 42);

        var entries = follower.GetLogEntries(-1);
        entries.Last().Should().BeEquivalentTo(new LogEntry(0, "key", 42));
        File.ReadLines(follower.LogFile).Last().Should().Be("0 key 42");
    }


    [Test]
    public void FollowerUpdatesNodesLogToMatchLeaderMultipleBehind()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        leader.AppendEntry(0, "key", 42);
        leader.AppendEntry(0, "key", 43);
        var gateway = new Gateway();
        gateway.Put("key", 44);

        var entries = follower.GetLogEntries(-1);

        entries[0].Should().BeEquivalentTo(new LogEntry(0, "key", 42));
        entries[1].Should().BeEquivalentTo(new LogEntry(0, "key", 43));
        entries[2].Should().BeEquivalentTo(new LogEntry(0, "key", 44));
    }

    [Test]
    public void LeaderBecomesFollowerIfTermIsHigher()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower,
            CurrentTerm = 1
        };

        leader.AppendEntry(0, "key", 42);
        leader.AppendEntry(0, "key", 43);
        var gateway = new Gateway();
        gateway.Put("key", 44);


        leader.Role.Should().Be(Role.Follower);
    }

    [Test]
    public void LeaderCommitsEntries()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        leader.AppendEntry(0, "key", 42);
        leader.AppendEntry(0, "key", 43);
        var gateway = new Gateway();
        gateway.Put("key", 44);

        leader.CommitIndex.Should().Be(2);
    }

    [Test]
    public void LeaderCommitsEntriesIfMajority()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        var follower2 = new Node
        {
            Role = Role.Follower
        };

        leader.AppendEntry(0, "key", 42);
        leader.AppendEntry(0, "key", 43);
        var gateway = new Gateway();
        gateway.Put("key", 44);

        leader.CommitIndex.Should().Be(2);
        follower.CommitIndex.Should().Be(2);
        follower2.CommitIndex.Should().Be(2);
    }

    [Test]
    public void LeaderCommitsEntriesIfMajority2()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        var follower2 = new Node
        {
            Role = Role.Follower
        };

        var follower3 = new Node
        {
            Role = Role.Follower,
            Healthy = false
        };

        leader.AppendEntry(0, "key", 42);
        leader.AppendEntry(0, "key", 43);
        var gateway = new Gateway();
        gateway.Put("key", 44);

        leader.CommitIndex.Should().Be(2);
        follower.CommitIndex.Should().Be(2);
        follower2.CommitIndex.Should().Be(2);
        follower3.CommitIndex.Should().Be(-1);
    }

    [Test]
    public void LeaderDoesNotCommitEntriesIfMajorityNotReached()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower,
            Healthy = false,
        };

        var follower2 = new Node
        {
            Role = Role.Follower,
            Healthy = false
        };

        var gateway = new Gateway();
        gateway.Put("key", 44);

        leader.CommitIndex.Should().Be(-1);
    }

    [Test]
    public void CorrectStateMachine()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower
        };

        var gateway = new Gateway();
        gateway.Put("key", 42);
        gateway.Put("key2", 43);

        leader.StateMachine["key"].Should().Be((42, 0));
        leader.StateMachine["key2"].Should().Be((43, 1));
        follower.StateMachine["key"].Should().Be((42, 0));
        follower.StateMachine["key2"].Should().Be((43, 1));
    }

    [Test]
    public void CorrectStateMachineNonMajority()
    {
        var leader = new Node
        {
            Role = Role.Leader
        };

        var follower = new Node
        {
            Role = Role.Follower,
        };

        var follower2 = new Node
        {
            Role = Role.Follower,
        };

        var gateway = new Gateway();
        gateway.Put("key", 42);

        follower.Healthy = false;
        follower2.Healthy = false;

        gateway.Put("key2", 43);

        leader.StateMachine["key"].Should().Be((42, 0));
        follower.StateMachine["key"].Should().Be((42, 0));
        follower2.StateMachine["key"].Should().Be((42, 0));

        leader.StateMachine.Keys.Should().NotContain("key2");
        follower.StateMachine.Keys.Should().NotContain("key2");
        follower2.StateMachine.Keys.Should().NotContain("key2");

        leader.CommitIndex.Should().Be(0);
        leader.LastApplied.Should().Be(0);

        follower.CommitIndex.Should().Be(0);
        follower.LastApplied.Should().Be(0);

        follower2.CommitIndex.Should().Be(0);
        follower2.LastApplied.Should().Be(0);
    }

    

    // [Test]
    // public void LeaderGetsElectedIfTwoOfThreeNodesAreHealthy()
    // {
    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();

    //     node1.DoAction(null, null);

    //     node1.Role.Should().Be(Role.Leader);
    // }

    // [Test]
    // public void LeaderGetsElectedIfThreeOfFiveNodesAreHealthy()
    // {
    //     Node.MinResetTimeout = int.MaxValue - 1;
    //     Node.MaxResetTimeout = int.MaxValue;

    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();
    //     var node4 = new Node();
    //     var node5 = new Node();

    //     node1.DoAction(null, null);

    //     node1.Role.Should().Be(Role.Leader);
    // }

    // [Test]
    // public void LeaderDoesNotGetElectedIfThreeOfFiveNodesAreUnhealthy()
    // {
    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();
    //     var node4 = new Node();
    //     var node5 = new Node();

    //     node2.Healthy = false;
    //     node3.Healthy = false;
    //     node4.Healthy = false;

    //     node1.DoAction(null, null);

    //     node1.Role.Should().Be(Role.Candidate);
    // }

    // [Test]
    // public void NodeWillContinueToBeLeaderIfAllNodesAreHealthy()
    // {
    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();

    //     node1.DoAction(null, null);
    //     node1.Role.Should().Be(Role.Leader);
    //     node1.DoAction(null, null);
    //     Node.Nodes.Where(n => n.Healthy).Count().Should().Be(3);
    //     node1.Role.Should().Be(Role.Leader);
    // }

    // [Test]
    // public void NodeWillCallForElectionIfMessagesFromLeaderTakeTooLong()
    // {
    //     Node.MinResetTimeout = 500;
    //     Node.MaxResetTimeout = 500;

    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();

    //     node1.DoAction(null, null);
    //     node1.Role.Should().Be(Role.Leader);

    //     Thread.Sleep(600);

    //     node1.Role.Should().NotBe(Role.Leader);
    // }

    // [Test]
    // public void NodeWillContinueAsLeaderEvenIfTwoNodesAreUnhealthy()
    // {
    //     var node1 = new Node();
    //     var node2 = new Node();
    //     var node3 = new Node();
    //     var node4 = new Node();
    //     var node5 = new Node();

    //     node1.DoAction(null, null);
    //     node1.Role.Should().Be(Role.Leader);

    //     node2.Healthy = false;
    //     node3.Healthy = false;

    //     node1.DoAction(null, null);
    //     node1.Role.Should().Be(Role.Leader);
    // }

    // [Test]
    // public void AvoidDoubleVoting()
    // {
    //     var a = new Node();
    //     var b = new Node();
    //     var c = new Node();
    //     var d = new Node();
    //     var e = new Node();

    //     a.CurrentTerm = 1;
    //     a.DoAction(null, null);
    //     a.Role.Should().Be(Role.Leader);

    //     b.CurrentTerm = 0;
    //     c.CurrentTerm = 0;
    //     d.CurrentTerm = 0;        
    //     b.Role = Role.Follower;
    //     c.Role = Role.Follower;
    //     d.Role = Role.Follower;

    //     e.CurrentTerm = 1;
    //     e.DoAction(null, null);

    //     e.Role.Should().Be(Role.Candidate);
    //     a.Role.Should().Be(Role.Leader);
    // }


    // [Test]
    // public void NodeWillStartAsFollower()
    // {
    //     var node = new Node();
    //     node.Role.Should().Be(Role.Follower);
    // }

    // [Test]
    // public void NodeWillBecomeFollowerIfLeaderIsElected()
    // {
    //     var node1 = new Node();
    //     node1.Role = Role.Leader;
    //     var node2 = new Node();
    //     node2.Role = Role.Candidate;
    //     var node3 = new Node();
    //     node3.Role = Role.Candidate;


    //     node1.DoAction(null, null);

    //     node1.Role.Should().Be(Role.Leader);
    //     node2.Role.Should().Be(Role.Follower);
    //     node3.Role.Should().Be(Role.Follower);
    // }

    // [Test]
    // public void OldLeaderWillBecomeFollowerIfNewLeaderIsElected()
    // {
    //     var node1 = new Node();
    //     node1.Role = Role.Leader;
    //     var node2 = new Node();
    //     var node3 = new Node();

    //     node2.DoAction(null, null); // Node 2 starts election and becomes leader
    //     node2.DoAction(null, null); // Node 2 sends heartbeat and tells node 1 to become follower

    //     node1.Role.Should().Be(Role.Follower);
    //     node2.Role.Should().Be(Role.Leader);
    //     node3.Role.Should().Be(Role.Follower);
    // }
}