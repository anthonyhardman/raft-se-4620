using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Logging.AddOpenTelemetry(options =>
{
    options.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://ah-otel-collector:4317");
    }).SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Raft.Gateway"));
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Raft.Gateway v1");
        o.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
