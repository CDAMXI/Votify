using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VotifyIU.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<INavigationHistoryService, NavigationHistoryService>();

await builder.Build().RunAsync();
