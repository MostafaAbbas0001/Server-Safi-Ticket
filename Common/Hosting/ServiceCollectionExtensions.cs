using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Common.Storage;
using Safi_Ticket.Data;
using Safi_Ticket.Email;
using Safi_Ticket.Email.Inbound;
using Safi_Ticket.Features.Auth;
using Safi_Ticket.Features.Lookups;
using Safi_Ticket.Features.Overview;
using Safi_Ticket.Features.Tickets;
using Safi_Ticket.Features.Users;

namespace Safi_Ticket.Common.Hosting;

public static class ServiceCollectionExtensions
{
    public const string FrontendPolicy = "FrontendPolicy";

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<HostOptions>(options =>
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost
        );

        services.AddCors(options =>
            options.AddPolicy(
                FrontendPolicy,
                policy =>
                    policy
                        .WithOrigins(GetFrontendOrigins(configuration))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
            )
        );

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddJwtAuthentication(configuration);
        services.AddAuthorization();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddProblemDetails();

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));

        // Features
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<LookupService>();
        services.AddScoped<OverviewService>();
        services.AddScoped<TicketQueries>();
        services.AddScoped<TicketService>();
        services.AddSingleton<AttachmentStorage>();

        // Email: outbound notifications, then the inbound mailbox reader.
        services.AddSingleton<EmailSender>();
        services.AddScoped<TicketNotifications>();
        services.AddSingleton<BackgroundEmailQueue>();
        services.AddHostedService(provider => provider.GetRequiredService<BackgroundEmailQueue>());
        services.AddScoped<EmailIngestionService>();
        services.AddHostedService<EmailReaderWorker>();

        return services;
    }

    private static void AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var jwtKey = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("JWT key is not configured.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "nameid",
                    RoleClaimType = "role",
                };
            });
    }

    private static string[] GetFrontendOrigins(IConfiguration configuration)
    {
        var allowedOrigins = configuration["Frontend:AllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(allowedOrigins))
        {
            return allowedOrigins.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
        }

        var baseUrl = configuration["Frontend:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl)
            ? ["http://127.0.0.1:8080", "http://localhost:8080"]
            : [baseUrl];
    }
}
