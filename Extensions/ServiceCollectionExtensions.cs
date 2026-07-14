using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Settings;
using Safi_Ticket.Services;

namespace Safi_Ticket.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private const string FrontendPolicy = "FrontendPolicy";

        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services.AddHostOptions();
            services.AddApplicationCors(configuration);
            services.AddDatabase(configuration);
            services.AddJwtAuthentication(configuration);
            services.AddAuthorization();
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddDomainServices();
            services.AddEmailServices(configuration);

            return services;
        }

        private static void AddHostOptions(this IServiceCollection services)
        {
            services.Configure<HostOptions>(options =>
            {
                options.BackgroundServiceExceptionBehavior =
                    BackgroundServiceExceptionBehavior.StopHost;
            });
        }

        private static void AddApplicationCors(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            var frontendOrigins = GetConfiguredFrontendOrigins(configuration);

            services.AddCors(options =>
            {
                options.AddPolicy(
                    FrontendPolicy,
                    policy =>
                    {
                        policy.WithOrigins(frontendOrigins).AllowAnyHeader().AllowAnyMethod();
                    }
                );
            });
        }

        private static void AddDatabase(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
            );
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

        private static void AddDomainServices(this IServiceCollection services)
        {
            services.AddScoped<AuthService>();
            services.AddScoped<UserService>();
            services.AddScoped<TicketService>();
            services.AddScoped<OverviewService>();
            services.AddScoped<StatusService>();
        }

        private static void AddEmailServices(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddScoped<EmailNotificationService>();
            services.AddScoped<EmailIngestionService>();
            services.AddSingleton<BackgroundEmailQueue>();
            services.AddHostedService(serviceProvider =>
                serviceProvider.GetRequiredService<BackgroundEmailQueue>()
            );
            services.AddHostedService<EmailReaderWorker>();
        }

        private static string[] GetConfiguredFrontendOrigins(IConfiguration configuration)
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
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                return new[] { baseUrl };
            }

            return new[] { "http://127.0.0.1:8080", "http://localhost:8080" };
        }
    }
}
