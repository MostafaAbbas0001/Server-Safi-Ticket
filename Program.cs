using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Common.Hosting;

EnvironmentConfiguration.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseApplicationPipeline();
await app.ApplyDatabaseMigrationsAsync();
app.Run();
