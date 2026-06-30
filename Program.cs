using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Settings;
using Safi_Ticket.Models;
using Safi_Ticket.Services;

var builder = WebApplication.CreateBuilder(args);

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
                .WithOrigins(
                    "http://127.0.0.1:8080",
                    "http://localhost:8080",
                    "http://172.17.184.39:8080"
                )
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

    var admin = await context.Users.FirstOrDefaultAsync(user => user.Email == "admin@safi.com");
    if (admin == null)
    {
        admin = new User
        {
            Name = "Alex Admin",
            Email = "admin@safi.com",
            PhoneNumber = "",
            RoleId = adminRole.Id,
        };

        context.Users.Add(admin);
    }
    else
    {
        admin.Name = "Alex Admin";
        admin.RoleId = adminRole.Id;
    }

    admin.HashedPassword = passwordHasher.HashPassword(admin, "Admin123!");

    await context.SaveChangesAsync();
}
