using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Settings;
using Safi_Ticket.Models;
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<TicketService>();
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

/*
 * Disable HTTPS redirection in local development because the frontend
 * is calling the backend through http://127.0.0.1:5044.
 *
 * If this stays enabled, POST requests like /api/Auth/login may redirect
 * to HTTPS and fail in the browser.
 */
// app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

await SeedDevelopmentAdminAsync(app);

app.Run();

static async Task SeedDevelopmentAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHasher = new PasswordHasher<User>();
    var seedAdmin = app.Configuration.GetSection("SeedAdmin");

    await context.Database.MigrateAsync();

    var adminRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == "Admin");
    if (adminRole == null)
    {
        adminRole = new Role { Name = "Admin" };
        context.Roles.Add(adminRole);
    }

    if (!await context.Roles.AnyAsync(role => role.Name == "User"))
    {
        context.Roles.Add(new Role { Name = "User" });
    }

    var requiredStatuses = new[]
    {
        "New",
        "Pending",
        "In Progress",
        "Resolved",
        "Closed",
        "Canceled",
    };

    foreach (var statusName in requiredStatuses)
    {
        if (!await context.Statuses.AnyAsync(status => status.Name == statusName))
        {
            context.Statuses.Add(new Status { Name = statusName });
        }
    }

    await context.SaveChangesAsync();

    var adminName = seedAdmin["Name"]?.Trim();
    var adminEmail = seedAdmin["Email"]?.Trim().ToLowerInvariant();
    var adminPassword = seedAdmin["Password"];
    var resetPassword = bool.TryParse(seedAdmin["ResetPassword"], out var shouldResetPassword)
        && shouldResetPassword;

    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning(
            "Seed admin skipped because SeedAdmin:Email or SeedAdmin:Password is missing."
        );
        return;
    }

    var admin = await context.Users.FirstOrDefaultAsync(user => user.Email == adminEmail);
    if (admin == null)
    {
        admin = new User
        {
            Name = string.IsNullOrWhiteSpace(adminName) ? "Safi Admin" : adminName,
            Email = adminEmail,
            PhoneNumber = "",
            RoleId = adminRole.Id,
        };

        admin.HashedPassword = passwordHasher.HashPassword(admin, adminPassword);
        context.Users.Add(admin);
    }
    else
    {
        admin.Name = string.IsNullOrWhiteSpace(adminName) ? admin.Name : adminName;
        admin.RoleId = adminRole.Id;

        if (resetPassword)
        {
            admin.HashedPassword = passwordHasher.HashPassword(admin, adminPassword);
        }
    }

    await context.SaveChangesAsync();
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

    SetFromEnvironmentIfMissing("SeedAdmin__Name", "SEED_ADMIN_NAME");
    SetFromEnvironmentIfMissing("SeedAdmin__Email", "SEED_ADMIN_EMAIL");
    SetFromEnvironmentIfMissing("SeedAdmin__Password", "SEED_ADMIN_PASSWORD");
    SetFromEnvironmentIfMissing("SeedAdmin__ResetPassword", "SEED_ADMIN_RESET_PASSWORD");
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
