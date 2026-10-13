# Render Deployment Guide with Upstash Redis

This guide covers deploying the Portfolio application to Render with Upstash Redis for production-grade caching and Data Protection key storage.

## Architecture

- **Web Service**: Render Web Service (Docker container)
- **Database**: Neon.tech PostgreSQL (or Render PostgreSQL)
- **Redis**: Upstash Redis (serverless, free tier available)

## Prerequisites

1. [Render account](https://render.com)
2. [Upstash account](https://upstash.com) (free tier includes 10,000 commands/day)
3. PostgreSQL database (Neon.tech or Render PostgreSQL)
4. Google OAuth credentials

## Step 1: Create Upstash Redis Database

1. Log in to [Upstash Console](https://console.upstash.com)
2. Click **Create Database**
3. Configure:
   - **Name**: `portfolio-redis`
   - **Region**: Choose closest to your Render region
   - **Type**: Regional (free tier)
   - **TLS**: Enabled (recommended)
4. Click **Create**

### Get Connection String

After creation, go to the **Details** tab and copy the **Redis URL** in one of these formats:

**Option A: rediss:// URI format** (recommended for Render environment variables):
```
rediss://default:YOUR_PASSWORD@region-endpoint.upstash.io:6380
```

**Option B: StackExchange.Redis format**:
```
region-endpoint.upstash.io:6380,password=YOUR_PASSWORD,ssl=True,abortConnect=False
```

Both formats are supported. The application automatically normalizes `rediss://` URLs.

## Step 2: Create Render Web Service

1. Log in to [Render Dashboard](https://dashboard.render.com)
2. Click **New +** → **Web Service**
3. Connect your GitHub repository (`brianpcoffey/gis-portfolio`)
4. Configure:

| Setting | Value |
|---------|-------|
| **Name** | `portfolio` |
| **Region** | Choose closest to Upstash Redis region |
| **Branch** | `main` |
| **Runtime** | `Docker` |
| **Dockerfile Path** | `Dockerfile` |
| **Docker Build Context Directory** | `.` (root) |
| **Instance Type** | `Starter` (512 MB RAM, $7/month) or `Free` (limited hours) |
| **Pre-Deploy Command** | `dotnet Portfolio.Web.dll --migrate` (see [Database migrations](#database-migrations-pre-deploy-step)) |
| **Auto-Deploy** | See [How a push to `main` reaches production](#how-a-push-to-main-reaches-production) |

## Step 3: Configure Environment Variables

In the Render dashboard, go to **Environment** and add these variables:

### Required Variables

| Variable | Value | Example |
|----------|-------|---------|
| `DATABASE_URL` | Your PostgreSQL connection string | `postgres://user:pass@host.neon.tech/portfolio` |
| `Redis__ConnectionString` | Upstash Redis URL from Step 1 | `rediss://default:password@region.upstash.io:6380` |
| `Authentication__Google__ClientId` | Your Google OAuth Client ID | `123456789-abc.apps.googleusercontent.com` |
| `Authentication__Google__ClientSecret` | Your Google OAuth Client Secret | `GOCSPX-xxxxxxxxxxxxx` |

### Optional Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Already set by Dockerfile |
| `ASPNETCORE_HTTP_PORTS` | `10000` | Set to a fixed `10000` by the Dockerfile (Render's default port); Render routes to it |
| `RunMigrationsOnStartup` | `true` | Set to `false` **only together with** the Pre-Deploy Command below |
| `BatchGeocoding__MaxConcurrency` | `4` | Max parallel geocoding requests per job |
| `BatchGeocoding__MinMatchScore` | `80` | Minimum score for geocode matches |
| `ForwardedHeaders__KnownNetworks` | *(empty)* | Comma-separated CIDRs of Render's proxy. Empty trusts `X-Forwarded-*` from any proxy; set it only after verifying Render's range (the forward limit stays 1 while it is empty) |

> **Note**: Use double underscores (`__`) in environment variable names. ASP.NET Core automatically maps `Redis__ConnectionString` to `Redis:ConnectionString` in `IConfiguration`.

### Database migrations (pre-deploy step)

By default the app applies EF Core migrations itself at startup (`db.Database.Migrate()`), which keeps a fresh clone working with no extra configuration. On an instance that spins down when idle that costs a round trip to the database on every cold start, and the first request after it is the one a visitor is waiting on. Two settings move the cost to deploy time; **set both or neither** (setting only `RunMigrationsOnStartup=false` means nothing migrates):

| Setting | Value |
|---------|-------|
| **Pre-Deploy Command** (Render service settings) | `dotnet Portfolio.Web.dll --migrate` — migrates, then exits without starting the web host; a failure exits non-zero, which aborts the deploy |
| `RunMigrationsOnStartup` (environment variable) | `false` — skips the startup pass |

If your Render plan does not offer a Pre-Deploy Command, leave both at their defaults and the app migrates itself at startup. These settings live in the Render dashboard, not in the repository, so nothing in the repo proves what a given service is set to.

## Step 4: Deploy

1. Click **Create Web Service**
2. Render will:
   - Clone your repository
   - Build the Docker image
   - Deploy the container
   - Assign a public URL: `https://portfolio-xxxx.onrender.com`

Monitor the deployment logs. You should see:

```
Redis ENABLED - Connection: region-endpoint.upstash.io:6380,password=***,ssl=True,abortConnect=False
Data Protection keys will be stored in Redis
Database migration successful.
```

With the pre-deploy step configured, `Database migration successful.` followed by `Migration-only run complete; exiting without starting the web host.` appears in the **pre-deploy** log, and the running service's log no longer shows the migration line.

## Step 5: Verify Redis Integration

### Check Logs

Look for these startup messages:

✅ **Redis enabled**:
```
Redis ENABLED - Connection: [masked]
Data Protection keys will be stored in Redis
```

❌ **Redis disabled** (fallback mode):
```
Redis DISABLED - using in-memory fallback for caching and job store
Data Protection keys will be stored in filesystem: /app/DataProtection-Keys
```

### Test Batch Geocoding

Batch geocoding jobs use Redis for state management. The CSV needs the columns `Id, Address, City, State, Zip`, is limited to 2 MB and 1,000 data rows, and the endpoint calls the paid ArcGIS geocoder (15 requests/minute/IP):

```bash
curl -X POST https://your-app.onrender.com/api/v1/geocoding/batch \
  -F "file=@addresses.csv"
```

Response (`202 Accepted`):
```json
{
  "jobId": "abc123",
  "statusUrl": "https://your-app.onrender.com/api/v1/geocoding/batch/abc123/status"
}
```

Jobs wait in a bounded in-process queue (capacity 8) drained by two workers; if it is full the API answers `503` with a `Retry-After` header. The queue is per process, but job state lives in Redis.

Poll status:
```bash
curl https://your-app.onrender.com/api/v1/geocoding/batch/abc123/status
```

If Redis is working, the job status will be accessible from any pod/replica.

### Test Data Protection

1. Log in via Google OAuth
2. Log out and log back in
3. ✅ If no antiforgery/correlation errors occur, Data Protection keys are working

Without Redis, you may see:
- "Antiforgery token validation failed"
- "OAuth correlation failed"
- "Unable to decrypt cookie"

## Troubleshooting

### Error: "Redis DISABLED" in production logs

**Cause**: `Redis__ConnectionString` is not set or is empty.

**Fix**:
1. Go to Render Dashboard → your service → **Environment**
2. Verify `Redis__ConnectionString` exists and has a value
3. Click **Save Changes** (triggers re-deploy)

### Error: "Failed to connect to Redis for Data Protection keys"

**Cause**: Invalid connection string or network issue.

**Fix**:
1. Verify the Upstash Redis connection string format
2. Check Upstash Console → Database → **Details** tab for correct endpoint
3. Ensure TLS is enabled (`rediss://` or `ssl=True`)
4. Test connection from Render shell (if available) or locally

### Error: "Antiforgery token validation failed"

**Cause**: Data Protection keys are not shared across instances or restarts.

**Fix**:
1. Verify Redis is enabled (check logs)
2. Ensure `Redis__ConnectionString` uses `rediss://` (with TLS)
3. Restart the service after adding/fixing Redis config

### High Redis command usage (Upstash free tier)

**Cause**: `IDistributedCache` is used for geocoding deduplication.

**Solutions**:
- Increase `CacheTtlMinutes` in `appsettings.json` (default: 60)
- Increase `CacheSlidingExpirationMinutes` for reverse geocoding (default: 30)
- Upgrade to Upstash paid tier if needed

## Render Free Tier Limitations

If using Render's **Free tier** (not Starter $7/month):

- **Service spins down after 15 minutes of inactivity**
- **750 hours/month limit** (shared across all free services)
- First request after spin-down takes 30-90 seconds (cold start)

Redis connection will be re-established automatically on cold start.

## Cost Estimate

| Service | Tier | Cost |
|---------|------|------|
| Render Web Service | Starter (512 MB) | $7/month |
| Render Web Service | Free | $0 (limited hours) |
| Upstash Redis | Free | $0 (10K commands/day) |
| Upstash Redis | Pay-as-you-go | ~$0.20 per 100K commands |
| Neon PostgreSQL | Free | $0 (0.5 GB storage) |

**Total minimum cost**: $0-7/month depending on Render tier choice.

## How a push to `main` reaches production

The repository's CI is written around two ways of deploying `main`, which are alternatives:

| Mode | What happens | Trade-off |
|------|--------------|-----------|
| **Render Auto-Deploy ON (on commit)** | Render builds every push to `main` as soon as GitHub reports it, without waiting for the GitHub Actions workflow. | Simplest, no secret needed. But a push deploys even if CI is red (the header comment in `ci.yml` says the same). |
| **Render Auto-Deploy OFF + `RENDER_DEPLOY_HOOK`** | The `deploy` job in `.github/workflows/ci.yml` POSTs Render's deploy hook, and only after the `test`, `native-parity` and `docker` jobs are green. | Only a green build ships. The cost is a dependency on the secret (Render → service → **Settings** → **Deploy Hook**, stored as a GitHub repository secret) and on CI. If the secret is missing the job prints "skipping" and exits 0, so a green `deploy` job does **not** prove a deploy fired — only the live site changing does. |

This project is meant to run in the second mode: the owner turned Auto-Deploy off and added the hook secret in July 2026, so CI's `deploy` job ships `main`. Those are dashboard and repository-secret settings, so the repo cannot confirm them. Either way, **treat a push to `main` as a production deploy.**

## Production Recommendations

1. ✅ Use Render **Starter** tier ($7/month) for always-on service
2. ✅ Pick one deploy mode deliberately (see above); do not rely on Auto-Deploy if you want a red build to be unable to ship
3. ✅ Use Upstash Redis with TLS (`rediss://`)
4. ✅ Store secrets in Render environment variables (not in code)
5. ✅ Monitor Upstash command usage in [Console](https://console.upstash.com)
6. ✅ Set `ASPNETCORE_ENVIRONMENT=Production` (already in Dockerfile)

## Support

- **Render Issues**: [Render Community](https://community.render.com)
- **Upstash Issues**: [Upstash Discord](https://upstash.com/discord)
- **App Issues**: Open a GitHub issue

