using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseApplicationPipeline(this WebApplication app)
        {
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
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapControllers();

            return app;
        }

        public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await context.Database.MigrateAsync();
        }
    }
}
