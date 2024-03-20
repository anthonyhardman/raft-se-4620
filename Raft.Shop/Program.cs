using Raft.Shop;
using Raft.Shop.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var storageUrl = System.Environment.GetEnvironmentVariable("STORAGE_SERVICE_URL") ?? "http://localhost:5001";

builder.Services.AddHttpClient("StorageClient", client =>
{
    client.BaseAddress = new Uri(storageUrl);
});

builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
