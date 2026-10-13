using Npgsql;

namespace Portfolio.Web.Infrastructure
{
    internal static class ConnectionStrings
    {
        // --------------------------
        // Helper: Resolve the database connection string
        // --------------------------
        // ConnectionStrings:DefaultConnection wins, then the DATABASE_URL environment variable
        // (the Render convention). A blank value counts as unset at each step: the committed
        // appsettings files carry "" precisely so that DATABASE_URL is reachable.
        internal static string ResolveDatabaseUrl(IConfiguration configuration, Func<string, string?> env)
        {
            var configured = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var fromEnv = env("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            throw new InvalidOperationException("Database connection string not set.");
        }

        // --------------------------
        // Helper: Parse postgres:// URI or standard connection string
        // --------------------------
        internal static NpgsqlConnectionStringBuilder ParsePostgres(string connectionString)
        {
            // If it's already a standard connection string (contains "Host="), use it directly
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                return new NpgsqlConnectionStringBuilder(connectionString);
            }

            // Parse URI format: postgres://user:pass@host:port/db or postgresql://user:pass@host/db
            if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            {
                // Never echo the input: a malformed value can still carry the password.
                throw new ArgumentException("Invalid connection string format.");
            }

            if (uri.Scheme is not ("postgres" or "postgresql"))
            {
                throw new ArgumentException($"Unsupported URI scheme: {uri.Scheme}. Expected 'postgres' or 'postgresql'.");
            }

            // Parse user info (user:password)
            var userInfo = uri.UserInfo.Split(':', 2);
            if (userInfo.Length < 2 || string.IsNullOrEmpty(userInfo[0]))
            {
                throw new ArgumentException("Connection string must include username and password.");
            }

            // URL-decode username and password (handles special characters like @ or %)
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = Uri.UnescapeDataString(userInfo[1]);

            // Default port to 5432 if not specified (uri.Port returns -1 when missing)
            var port = uri.Port > 0 ? uri.Port : 5432;

            // Handle IPv6 localhost
            var host = uri.Host;
            if (host == "[::1]")
            {
                host = "127.0.0.1";
            }

            // Extract database name from path
            var database = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(database))
            {
                throw new ArgumentException("Database name is required in the connection string.");
            }

            // Parse query string for additional options (e.g., ?sslmode=require)
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

            var npgsqlBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                Database = database,
                SslMode = SslMode.Require
            };

            // Override SSL mode if specified in query string
            if (queryParams["sslmode"] is string sslModeValue &&
                Enum.TryParse<SslMode>(sslModeValue, ignoreCase: true, out var sslMode))
            {
                npgsqlBuilder.SslMode = sslMode;
            }

            return npgsqlBuilder;
        }

        // --------------------------
        // Helper: Normalize Redis connection string
        // --------------------------
        internal static string NormalizeRedis(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            connectionString = connectionString.Trim();

            // If it's already in StackExchange.Redis format (contains comma-separated options), return as-is
            if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
            {
                return connectionString;
            }

            // Parse rediss:// or redis:// URI format (Upstash/Redis Cloud style)
            if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            {
                // Never echo the input: a malformed value can still carry the password.
                throw new ArgumentException("Invalid Redis connection string format.");
            }

            var useSsl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (useSsl ? 6380 : 6379);
            var password = string.IsNullOrEmpty(uri.UserInfo) ? null : Uri.UnescapeDataString(uri.UserInfo.Split(':').Last());

            // Build StackExchange.Redis-compatible connection string
            var configOptions = new List<string>
            {
                $"{host}:{port}"
            };

            if (!string.IsNullOrEmpty(password))
            {
                configOptions.Add($"password={password}");
            }

            if (useSsl)
            {
                configOptions.Add("ssl=True");
            }

            configOptions.Add("abortConnect=False");

            return string.Join(",", configOptions);
        }

        // --------------------------
        // Helper: Mask Redis password for logging
        // --------------------------
        internal static string MaskRedisPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            // Mask password= portion
            var parts = connectionString.Split(',');
            var masked = parts.Select(part =>
            {
                if (part.Trim().StartsWith("password=", StringComparison.OrdinalIgnoreCase))
                    return "password=***";
                return part;
            });

            return string.Join(",", masked);
        }
    }
}
