using Npgsql;
using Portfolio.Web.Infrastructure;

namespace Portfolio.Tests.Infrastructure
{
    /// <summary>
    /// Characterization tests for the connection-string helpers extracted from Program.cs.
    /// All values are fake test vectors.
    /// </summary>
    public class ConnectionStringsTests
    {
        // ---------- Postgres ----------

        [Fact]
        public void ParsePostgres_HostFormat_PassesThroughUnchanged()
        {
            var builder = ConnectionStrings.ParsePostgres("Host=db.example.com;Port=5433;Database=app;Username=u;Password=p");

            Assert.Equal("db.example.com", builder.Host);
            Assert.Equal(5433, builder.Port);
            Assert.Equal("app", builder.Database);
            Assert.Equal("u", builder.Username);
            Assert.Equal("p", builder.Password);
        }

        [Fact]
        public void ParsePostgres_Uri_ParsesAllPartsAndHonoursSslMode()
        {
            var builder = ConnectionStrings.ParsePostgres("postgres://u:p%40ss@db.example.com:6543/app?sslmode=disable");

            Assert.Equal("db.example.com", builder.Host);
            Assert.Equal(6543, builder.Port);
            Assert.Equal("u", builder.Username);
            Assert.Equal("p@ss", builder.Password);
            Assert.Equal("app", builder.Database);
            Assert.Equal(SslMode.Disable, builder.SslMode);
        }

        [Fact]
        public void ParsePostgres_PostgresqlScheme_DefaultsPortAndRequiresSsl()
        {
            var builder = ConnectionStrings.ParsePostgres("postgresql://u:p@host/db");

            Assert.Equal(5432, builder.Port);
            Assert.Equal(SslMode.Require, builder.SslMode);
            Assert.Equal("host", builder.Host);
            Assert.Equal("db", builder.Database);
        }

        [Fact]
        public void ParsePostgres_Ipv6Loopback_BecomesIpv4Loopback()
        {
            var builder = ConnectionStrings.ParsePostgres("postgres://u:p@[::1]/db");

            Assert.Equal("127.0.0.1", builder.Host);
        }

        [Fact]
        public void ParsePostgres_MissingDatabase_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => ConnectionStrings.ParsePostgres("postgres://u:p@host"));
            Assert.Equal("Database name is required in the connection string.", ex.Message);
        }

        [Fact]
        public void ParsePostgres_MissingPassword_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => ConnectionStrings.ParsePostgres("postgres://u@host/db"));
            Assert.Equal("Connection string must include username and password.", ex.Message);
        }

        [Fact]
        public void ParsePostgres_WrongScheme_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => ConnectionStrings.ParsePostgres("mysql://u:p@host/db"));
            Assert.Equal("Unsupported URI scheme: mysql. Expected 'postgres' or 'postgresql'.", ex.Message);
        }

        [Fact]
        public void ParsePostgres_Malformed_ThrowsAndMessageCurrentlyContainsRawString()
        {
            // Pins CURRENT behaviour: the message echoes the raw input, which can leak a
            // password. WP-14 changes this; its fix flips this assertion.
            const string raw = "not a uri:secretpw";

            var ex = Assert.Throws<ArgumentException>(() => ConnectionStrings.ParsePostgres(raw));

            Assert.Equal($"Invalid connection string format: {raw}", ex.Message);
            Assert.Contains(raw, ex.Message);
        }

        // ---------- Redis ----------

        [Fact]
        public void NormalizeRedis_EmptyOrWhitespace_ReturnsEmpty()
        {
            Assert.Equal("", ConnectionStrings.NormalizeRedis(""));
            Assert.Equal("", ConnectionStrings.NormalizeRedis("   "));
        }

        [Fact]
        public void NormalizeRedis_StackExchangeFormat_PassesThrough()
        {
            Assert.Equal("h:6379,password=x", ConnectionStrings.NormalizeRedis("h:6379,password=x"));
        }

        [Fact]
        public void NormalizeRedis_RedisUri_ConvertsWithPassword()
        {
            Assert.Equal("h:6379,password=pw,abortConnect=False", ConnectionStrings.NormalizeRedis("redis://:pw@h"));
        }

        [Fact]
        public void NormalizeRedis_RedissUriWithUserAndPort_EnablesSsl()
        {
            Assert.Equal("h:6380,password=pw,ssl=True,abortConnect=False",
                ConnectionStrings.NormalizeRedis("rediss://default:pw@h:6380"));
        }

        [Fact]
        public void NormalizeRedis_RedissWithoutPassword_DefaultsSslPort()
        {
            Assert.Equal("h:6380,ssl=True,abortConnect=False", ConnectionStrings.NormalizeRedis("rediss://h"));
        }

        // ---------- Mask ----------

        [Fact]
        public void MaskRedisPassword_MasksPasswordPart()
        {
            Assert.Equal("h:6380,password=***,ssl=True", ConnectionStrings.MaskRedisPassword("h:6380,password=pw,ssl=True"));
        }

        [Fact]
        public void MaskRedisPassword_MatchingIsCaseInsensitive()
        {
            Assert.Equal("h:6380,password=***,ssl=True", ConnectionStrings.MaskRedisPassword("h:6380,PassWord=pw,ssl=True"));
        }

        [Fact]
        public void MaskRedisPassword_EmptyOrWhitespace_ReturnsEmpty()
        {
            Assert.Equal("", ConnectionStrings.MaskRedisPassword(""));
            Assert.Equal("", ConnectionStrings.MaskRedisPassword("  "));
        }
    }
}
