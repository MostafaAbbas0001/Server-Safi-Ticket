using Safi_Ticket.Configuration;
using Safi_Ticket.Extensions;

EnvironmentConfiguration.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddApplicationLogging();
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseApplicationPipeline();
await app.ApplyDatabaseMigrationsAsync();
app.Run();
