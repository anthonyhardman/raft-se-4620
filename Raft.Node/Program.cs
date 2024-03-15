using Raft.Node;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var nodeId = Environment.GetEnvironmentVariable("NODE_ID") ?? "node1";
var nodes = Environment.GetEnvironmentVariable("NODES")?.Split(",") ?? new string[] { "node1" };
var node = new Node(nodeId, nodes.ToList());
builder.Services.AddSingleton<Node>(sp =>
{
  return node;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(o =>
  {
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Raft.Gateway v1");
    o.RoutePrefix = string.Empty;
  });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
