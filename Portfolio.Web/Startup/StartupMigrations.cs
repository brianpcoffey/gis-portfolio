using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories;

namespace Portfolio.Web.Startup;

internal static class StartupMigrations
{
    // --------------------------
    // Apply Migrations Safely
    // --------------------------
    // Migrating on startup is the right default: it keeps a fresh clone and any host
    // without a deploy step working with no extra configuration.
    //
    // It is the wrong thing in production on a free instance, though. The container
    // spins down when idle, so every cold start pays a round trip to Neon to check the
    // migration history before the app can answer its first request — and that request
    // is the one a recruiter is waiting on. Two opt-in switches move that cost to
    // deploy time:
    //
    //   dotnet Portfolio.Web.dll --migrate   migrate, then exit without starting the
    //                                        web host (Render's Pre-Deploy Command)
    //   RunMigrationsOnStartup=false         skip the startup pass entirely
    //
    // Set both together, or neither. Setting only the env var means nothing migrates.
    /// <returns>An exit code when the process should stop here, or <c>null</c> to continue to <c>app.Run()</c>.</returns>
    internal static int? Run(WebApplication app, string[] args, IConfiguration configuration, ILogger logger)
    {
        var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
        var migrateOnStartup = configuration.GetValue("RunMigrationsOnStartup", true);

        if (migrateOnly || migrateOnStartup)
        {
            using var scope = app.Services.CreateScope();
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
                db.Database.Migrate();
                logger.LogInformation("Database migration successful.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed.");

                // A startup failure is logged and tolerated, as before — the app may still
                // serve pages that do not touch the database. A --migrate failure must not
                // be swallowed: Render aborts the deploy on a non-zero exit, which is
                // exactly the behaviour wanted when the schema could not be brought up.
                if (migrateOnly)
                    return 1;
            }
        }

        if (migrateOnly)
        {
            logger.LogInformation("Migration-only run complete; exiting without starting the web host.");
            return 0;
        }

        return null;
    }
}
