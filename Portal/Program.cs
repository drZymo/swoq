using Swoq.Data;
using Swoq.Portal.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:80", "https://*:443");

builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabase>();
builder.Services.AddScoped<UserService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
