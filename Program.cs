using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Settings;
using Safi_Ticket.Services;

LoadEnvFile();
ApplyEnvironmentConfigurationAliases();

var builder = WebApplication.CreateBuilder(args);
var frontendOrigins = GetConfiguredFrontendOrigins(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "FrontendPolicy",
        policy =>
        {
            policy
                .WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<OverviewService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<AuthService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddScoped<EmailIngestionService>();
builder.Services.AddSingleton<TicketEventNotifier>();
builder.Services.AddSingleton<BackgroundEmailQueue>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<BackgroundEmailQueue>());
builder.Services.AddHostedService<EmailReaderWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.InjectStylesheet("/swagger-ui/custom.css");
    });
}

app.UseStaticFiles();

app.UseCors("FrontendPolicy");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

await ApplyDatabaseMigrationsAsync(app);

app.Run();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();
}

static void LoadEnvFile()
{
    foreach (var envPath in GetEnvFileCandidates())
    {
        if (!File.Exists(envPath))
        {
            continue;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (
                value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            )
            {
                value = value[1..^1];
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return;
    }
}

static string[] GetEnvFileCandidates()
{
    var currentDirectory = Directory.GetCurrentDirectory();

    return new[]
    {
        Path.Combine(currentDirectory, ".env"),
        Path.Combine(currentDirectory, "Server", ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env"),
    };
}

static void ApplyEnvironmentConfigurationAliases()
{
    SetFromEnvironmentIfMissing("Jwt__Key", "JWT_KEY");
    SetFromEnvironmentIfMissing("Jwt__Issuer", "JWT_ISSUER");
    SetFromEnvironmentIfMissing("Jwt__Audience", "JWT_AUDIENCE");
    SetFromEnvironmentIfMissing("Jwt__ExpireMinutes", "JWT_EXPIRE_MINUTES");

    SetFromEnvironmentIfMissing("EmailSettings__Host", "EMAIL_HOST");
    SetFromEnvironmentIfMissing("EmailSettings__Port", "EMAIL_PORT");
    SetFromEnvironmentIfMissing("EmailSettings__Username", "EMAIL_USERNAME");
    SetFromEnvironmentIfMissing("EmailSettings__Password", "EMAIL_PASSWORD");
    SetFromEnvironmentIfMissing("EmailSettings__Mailbox", "EMAIL_MAILBOX");
    SetFromEnvironmentIfMissing("EmailSettings__PollSeconds", "EMAIL_POLL_SECONDS");
    SetFromEnvironmentIfMissing("EmailSettings__SmtpHost", "EMAIL_SMTP_HOST");
    SetFromEnvironmentIfMissing("EmailSettings__SmtpPort", "EMAIL_SMTP_PORT");
    SetFromEnvironmentIfMissing("EmailSettings__FromName", "EMAIL_FROM_NAME");

    SetFromEnvironmentIfMissing("Frontend__BaseUrl", "FRONTEND_BASE_URL");
    SetFromEnvironmentIfMissing("Frontend__AllowedOrigins", "FRONTEND_ALLOWED_ORIGINS");

    SetFromEnvironmentIfMissing("AllowedHosts", "ALLOWED_HOSTS");

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
    {
        var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (!string.IsNullOrWhiteSpace(postgresPassword))
        {
            var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "SafiTicket";
            var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "safi";

            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                $"Host={postgresHost};Port={postgresPort};Database={postgresDb};Username={postgresUser};Password={postgresPassword}"
            );
        }
    }
}

static void SetFromEnvironmentIfMissing(string configurationKey, string environmentKey)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(configurationKey)))
    {
        return;
    }

    var value = Environment.GetEnvironmentVariable(environmentKey);
    if (!string.IsNullOrWhiteSpace(value))
    {
        Environment.SetEnvironmentVariable(configurationKey, value);
    }
}

static string[] GetConfiguredFrontendOrigins(IConfiguration configuration)
{
    var allowedOrigins = configuration["Frontend:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(allowedOrigins))
    {
        return allowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    var baseUrl = configuration["Frontend:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        return new[] { baseUrl };
    }

    return new[]
    {
        "http://127.0.0.1:8080",
        "http://localhost:8080",
    };
}
