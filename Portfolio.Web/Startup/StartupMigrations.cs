using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories;
using Portfolio.Repositories.Maintenance;

namespace Portfolio.Web.Startup;

internal static class StartupMigrations
{
    // The generated migration ids carry a timestamp prefix, so they are matched by name.
    internal const string UniqueOrderNumberMigrationSuffix = "_UniqueOrderNumberPerUser";
    internal const string InitialCreateMigrationSuffix = "_InitialCreate";

    // Migrating on startup is the right default: it keeps a fresh clone and any host without a
    // deploy step working with no extra configuration. On a free instance in production it is
    // the wrong one: the container spins down when idle, so every cold start pays round trips to
    // Neon to check the migration history before the first request, and that request is the one a
    // recruiter is waiting on. Two opt-in switches move that cost to deploy time:
    //   dotnet Portfolio.Web.dll --migrate   migrate, then exit without starting the web host
    //                                        (Render's Pre-Deploy Command)
    //   RunMigrationsOnStartup=false         skip the startup pass entirely
    // Set both together, or neither: with only the env var, nothing migrates.
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
                RenumberDuplicateOrderNumbers(db, logger);
                db.Database.Migrate();
                logger.LogInformation("Database migration successful.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed.");

                // A startup failure is logged and tolerated — the app may still
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

    // UniqueOrderNumberPerUser creates a unique index, and CREATE UNIQUE INDEX fails when one user's orders already
    // share a number (a race between two creates could issue the same number before the index existed). A generated
    // migration cannot carry data SQL, so the duplicates are renumbered here, in the same process just before
    // Migrate(), which also covers --migrate. It runs only while that migration is pending: once it is applied the
    // index itself prevents duplicates. If the other (old) instance inserts a duplicate between this step and the
    // CREATE INDEX during a deploy overlap, the migration fails and is tolerated at startup; it is still pending on
    // the next start, so this step runs again.
    private static void RenumberDuplicateOrderNumbers(PortfolioDbContext db, ILogger logger)
    {
        if (!db.Database.IsRelational() || !ShouldDedupe(db.Database.GetPendingMigrations().ToList()))
            return;

        var renumbered = FiberOrderNumberDedupe.Run(db);
        logger.LogInformation("Renumbered {Count} duplicate order numbers before migrating.", renumbered);
    }

    /// <summary>
    /// True when <c>UniqueOrderNumberPerUser</c> is about to be applied to a database that already has the
    /// <c>FiberOrders</c> table. On a fresh database <c>InitialCreate</c> is pending too, the table does not exist
    /// yet and there is nothing to renumber.
    /// </summary>
    internal static bool ShouldDedupe(IReadOnlyCollection<string> pendingMigrations) =>
        pendingMigrations.Any(id => id.EndsWith(UniqueOrderNumberMigrationSuffix, StringComparison.Ordinal))
        && !pendingMigrations.Any(id => id.EndsWith(InitialCreateMigrationSuffix, StringComparison.Ordinal));
}
