using CloudflareDdns.Components;
using CloudflareDdns.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var ddnsSettings = builder.Configuration.GetSection("Ddns").Get<DdnsSettings>() ?? new DdnsSettings();
builder.Services.AddSingleton(ddnsSettings);
builder.Services.AddSingleton<DdnsState>();
builder.Services.AddHttpClient<CloudflareService>(client =>
{
    client.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ddnsSettings.CloudflareToken);
});
builder.Services.AddHttpClient("Ipify", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("HeDns", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<DdnsBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
