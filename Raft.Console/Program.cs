using Raft.Console;

var threads = new List<Thread>();

// for (int i = 0; i < 5; i++)
// {
//     var node = new Node();
//     var thread = new Thread(node.Run);
//     Console.WriteLine($"Node {node.Id} started as {node.Role}");
//     thread.Start();
//     threads.Add(thread);
// }

var node1 = new Node();

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// Node.StopAll();

foreach (var thread in threads)
{
    thread.Join();
}