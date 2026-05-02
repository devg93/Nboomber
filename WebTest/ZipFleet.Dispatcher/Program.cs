using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZipFleet.Dispatcher;
using ZipFleet.Dispatcher.Models;
using ZipFleet.Dispatcher.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>() ?? new ApiSettings();

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiSettings.BaseUrl) });
builder.Services.AddScoped<ApiService>();
builder.Services.AddSingleton<SignalRService>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton(apiSettings);

await builder.Build().RunAsync();
