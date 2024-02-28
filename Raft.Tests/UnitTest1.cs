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
        Node.Reset();
    }


    [Test]
    public void LeaderGetsElectedIfTwoOfThreeNodesAreHealthy()
    {
        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();

        node1.DoAction(null, null);

        node1.Role.Should().Be(Role.Leader);
    }

    [Test]
    public void LeaderGetsElectedIfThreeOfFiveNodesAreHealthy()
    {
        Node.MinResetTimeout = int.MaxValue - 1;
        Node.MaxResetTimeout = int.MaxValue;

        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();
        var node4 = new Node();
        var node5 = new Node();

        node1.DoAction(null, null);

        node1.Role.Should().Be(Role.Leader);
    }

    [Test]
    public void LeaderDoesNotGetElectedIfThreeOfFiveNodesAreUnhealthy()
    {
        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();
        var node4 = new Node();
        var node5 = new Node();

        node2.Healthy = false;
        node3.Healthy = false;
        node4.Healthy = false;

        node1.DoAction(null, null);

        node1.Role.Should().Be(Role.Candidate);
    }

    [Test]
    public void NodeWillContinueToBeLeaderIfAllNodesAreHealthy()
    {
        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();

        node1.DoAction(null, null);
        node1.Role.Should().Be(Role.Leader);
        node1.DoAction(null, null);
        Node.Nodes.Where(n => n.Healthy).Count().Should().Be(3);
        node1.Role.Should().Be(Role.Leader);
    }

    [Test]
    public void NodeWillCallForElectionIfMessagesFromLeaderTakeTooLong()
    {
        Node.MinResetTimeout = 500;
        Node.MaxResetTimeout = 500;

        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();

        node1.DoAction(null, null);
        node1.Role.Should().Be(Role.Leader);

        Thread.Sleep(600);

        node1.Role.Should().NotBe(Role.Leader);
    }

    [Test]
    public void NodeWillContinueAsLeaderEvenIfTwoNodesAreUnhealthy()
    {
        var node1 = new Node();
        var node2 = new Node();
        var node3 = new Node();
        var node4 = new Node();
        var node5 = new Node();

        node1.DoAction(null, null);
        node1.Role.Should().Be(Role.Leader);

        node2.Healthy = false;
        node3.Healthy = false;

        node1.DoAction(null, null);
        node1.Role.Should().Be(Role.Leader);
    }

    [Test]
    public void AvoidDoubleVoting()
    {
        var a = new Node();
        var b = new Node();
        var c = new Node();
        var d = new Node();
        var e = new Node();

        a.CurrentTerm = 1;
        a.DoAction(null, null);
        a.Role.Should().Be(Role.Leader);

        b.CurrentTerm = 0;
        c.CurrentTerm = 0;
        d.CurrentTerm = 0;        
        b.Role = Role.Follower;
        c.Role = Role.Follower;
        d.Role = Role.Follower;

        e.CurrentTerm = 1;
        e.DoAction(null, null);

        e.Role.Should().Be(Role.Candidate);
        a.Role.Should().Be(Role.Leader);
    }


    [Test]
    public void NodeWillStartAsFollower()
    {
        var node = new Node();
        node.Role.Should().Be(Role.Follower);
    }

    [Test]
    public void NodeWillBecomeFollowerIfLeaderIsElected()
    {
        var node1 = new Node();
        node1.Role = Role.Leader;
        var node2 = new Node();
        node2.Role = Role.Candidate;
        var node3 = new Node();
        node3.Role = Role.Candidate;


        node1.DoAction(null, null);

        node1.Role.Should().Be(Role.Leader);
        node2.Role.Should().Be(Role.Follower);
        node3.Role.Should().Be(Role.Follower);
    }

    [Test]
    public void OldLeaderWillBecomeFollowerIfNewLeaderIsElected()
    {
        var node1 = new Node();
        node1.Role = Role.Leader;
        var node2 = new Node();
        var node3 = new Node();

        node2.DoAction(null, null); // Node 2 starts election and becomes leader
        node2.DoAction(null, null); // Node 2 sends heartbeat and tells node 1 to become follower

        node1.Role.Should().Be(Role.Follower);
        node2.Role.Should().Be(Role.Leader);
        node3.Role.Should().Be(Role.Follower);
    }
}