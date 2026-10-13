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
| **Pre-Deploy Command** | Leave empty. On the free plan the field is locked, and the app migrates itself at startup (see [Database migrations](#database-migrations)) |
| **Health Check Path** | `/health/live` (see [Health check](#health-check)) |
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
| `RunMigrationsOnStartup` | `true` | Leave at the default on the free plan. Set to `false` **only together with** a Pre-Deploy Command (see [Database migrations](#database-migrations)) |
| `BatchGeocoding__MaxConcurrency` | `4` | Max parallel geocoding requests per job |
| `BatchGeocoding__MinMatchScore` | `80` | Minimum score for geocode matches |
| `ForwardedHeaders__KnownNetworks` | *(empty)* | Comma-separated CIDRs of Render's proxy. Empty trusts `X-Forwarded-*` from any proxy; set it only after verifying Render's range (the forward limit stays 1 while it is empty) |

> **Note**: Use double underscores (`__`) in environment variable names. ASP.NET Core automatically maps `Redis__ConnectionString` to `Redis:ConnectionString` in `IConfiguration`.

### Database migrations

**What happens today.** The service runs on Render's free plan, where the **Pre-Deploy Command** field in the service settings is locked and empty (observed in the dashboard in October 2026; the repository cannot confirm dashboard settings). So nothing migrates before a deploy. Instead the app migrates itself at startup: `StartupMigrations.Run` calls `db.Database.Migrate()` unless the `RunMigrationsOnStartup` setting is `false`. That setting defaults to `true`, and the service logs show the migration line at startup (observed in the Render logs, October 2026), so it has not been turned off. Every process start (each deploy, and each cold start after the free instance spins down when idle) logs `Database migration successful.` and pays one round trip to Neon to check the migration history before serving its first request.

Two behaviours follow from that:

- A migration failure at startup is logged (`Database migration failed.`) and tolerated: the process keeps running and can still serve pages that do not touch the database. It does not abort the deploy.
- Nothing needs to be configured. A fresh clone and a Render free service both work with the defaults.

**On a paid plan** the Pre-Deploy Command field should be editable (the free plan locks it), so the cost could move from every cold start to deploy time. Two settings do that, and you **set both or neither** (setting only `RunMigrationsOnStartup=false` means nothing migrates):

| Setting | Value |
|---------|-------|
| **Pre-Deploy Command** (Render service settings) | `dotnet Portfolio.Web.dll --migrate`: migrates, then exits without starting the web host; a failure exits non-zero, which aborts the deploy |
| `RunMigrationsOnStartup` (environment variable) | `false`: skips the startup pass |

### Health check

The app exposes two anonymous probe endpoints that are exempt from rate limiting:

| Path | Checks | Use |
|------|--------|-----|
| `/health/live` | None: it answers `200` while the process is up | Render's Health Check Path |
| `/health/ready` | Database connectivity (the result is cached for 10 seconds) | Kubernetes readiness probes (`k8s/`) |

The service currently has **no Health Check Path set** in Render (observed in the dashboard in October 2026), so Render applies no application-level check to a new deploy or to a running instance. Set **Health Check Path** to `/health/live` (service **Settings** → **Health Check Path**). Prefer it to `/health/ready` on Render: the readiness check queries the database, a brief Neon outage would then take the instance out of rotation although the app is fine, and on a serverless database each probe can wake the compute.

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

On the free plan the migration line appears in the running service's log on every start. If you later move migrations to a paid plan's pre-deploy step, `Database migration successful.` followed by `Migration-only run complete; exiting without starting the web host.` appears in the **pre-deploy** log instead, and the running service's log no longer shows the migration line.

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

## Cold starts and the keep-warm ping

On the free plan Render stops the service after 15 minutes without inbound HTTP, and the next request waits about a minute while it starts. The workflow `.github/workflows/keep-warm.yml` reduces that during working hours: a scheduled GitHub Actions job sends `GET https://brianpcoffey.com/health/live` so the instance stays up. Outside the window the free instance is still allowed to sleep.

| Setting | Value |
|---------|-------|
| Window | 12:00 to 03:59 UTC, every day |
| Cadence | every 5 minutes, at minutes 3 to 58 (runs at the top of the hour are avoided because GitHub delays them) |
| Attempts | up to three per run; each waits up to 75 seconds for a response, with 10 seconds between attempts |
| Failed run | a warning annotation; the next run retries |
| Strict run | 15:41 UTC daily, and manual runs: a failure fails the run and notifies |

The window is fixed in UTC, so local times move with daylight saving. In summer (EDT and PDT) it is 8 AM to midnight Eastern and 5 AM to 8:59 PM Pacific; in winter it starts and ends one hour earlier.

**Budget.** The free plan includes 750 instance-hours per workspace per month, shared by every free service. Pinging for 16 hours a day uses about 500 of them in a 31-day month. Before relying on this, check the Render dashboard under **Billing** → **Monthly Included Usage**. If other free services share the workspace, or usage nears the limit, the alternative is the Starter plan ($7/month), which removes spin-down.

**Acceptable use.** Render's acceptable-use policy does not mention keep-alive pings. The ping stays inside working hours as the conservative reading of that policy. If Render objects, disable the workflow and move to Starter.

**Turning it off and on.** Disable it with `gh workflow disable keep-warm.yml` or from the Actions tab; re-enable it with `gh workflow enable keep-warm.yml`. GitHub disables scheduled workflows in a public repository after 60 days without repository activity, so the workflow can stop on its own. It then shows as disabled in the Actions tab; re-enable it with the command above.

**Failure notifications.** A failed strict run notifies the person who last edited the cron lines, subject to their Actions notification settings. Warning-only runs do not fail, so they send no email. To see recent runs, use `gh run list --workflow keep-warm.yml`.

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
7. ✅ Set the Health Check Path to `/health/live` (see [Health check](#health-check))

## Support

- **Render Issues**: [Render Community](https://community.render.com)
- **Upstash Issues**: [Upstash Discord](https://upstash.com/discord)
- **App Issues**: Open a GitHub issue

