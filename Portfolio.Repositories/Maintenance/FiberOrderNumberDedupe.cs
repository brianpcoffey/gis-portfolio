using Microsoft.EntityFrameworkCore;

namespace Portfolio.Repositories.Maintenance;

/// <summary>
/// Renumbers duplicate fiber order numbers so the unique (user, order number) index can be created.
/// </summary>
/// <remarks>
/// A generated EF migration holds schema operations only, so this data fix cannot live inside
/// <c>UniqueOrderNumberPerUser</c>. The startup migration step runs it just before <c>Migrate()</c>, and only while
/// that migration is still pending. Two processes running it at the same time could give one row a double suffix
/// (<c>-57-57</c>); the result is still unique, and production runs a single instance.
/// </remarks>
public static class FiberOrderNumberDedupe
{
    // Within each user's group of identical numbers the lowest id keeps its number; every other row becomes
    // "<number>-<id>", e.g. ORD-20261013-0003-57. The suffix is not a digit run, so the order service's sequence
    // parser ignores a renumbered row. Blank numbers are skipped, as the index skips them.
    private const string Sql = """
        UPDATE "FiberOrders" AS o SET order_number = o.order_number || '-' || o.id
        FROM (SELECT id, row_number() OVER (PARTITION BY user_id, order_number ORDER BY id) AS rn
              FROM "FiberOrders" WHERE order_number <> '') AS d
        WHERE o.id = d.id AND d.rn > 1;
        """;

    /// <summary>Renumbers every duplicate and returns how many rows were changed. Runs as one statement.</summary>
    public static int Run(PortfolioDbContext db) => db.Database.ExecuteSqlRaw(Sql);
}
