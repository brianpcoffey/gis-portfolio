using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Portfolio.Web.Controllers
{
    /// <summary>Public sink for Content-Security-Policy violation reports (report-uri / report-to).</summary>
    /// <remarks>
    /// Accepts <c>application/csp-report</c> and <c>application/reports+json</c>, logs one sanitised Warning line per
    /// violation and answers 204. Hidden from OpenAPI.
    /// </remarks>
    [ApiController]
    [ApiVersionNeutral]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("csp")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("csp-report")]
    public class CspReportController : ControllerBase
    {
        private const int MaxBodyBytes = 8192;
        private const int MaxFieldLength = 200;
        private const int MaxReportsPerRequest = 10;

        private readonly ILogger<CspReportController> _logger;

        public CspReportController(ILogger<CspReportController> logger)
        {
            _logger = logger;
        }

        /// <summary>Receives a CSP violation report.</summary>
        [HttpPost]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Report(CancellationToken cancellationToken)
        {
            var contentType = Request.ContentType?.Split(';')[0].Trim();
            if (!string.Equals(contentType, "application/csp-report", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(contentType, "application/reports+json", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            if (Request.ContentLength > MaxBodyBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            byte[] body;
            try
            {
                body = await ReadBodyAsync(cancellationToken);
            }
            catch (BadHttpRequestException ex)
            {
                return StatusCode(ex.StatusCode);
            }
            if (body.Length > MaxBodyBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                foreach (var report in Extract(doc.RootElement).Take(MaxReportsPerRequest))
                {
                    if (IsKnownArcGisEval(report))
                    {
                        continue;
                    }

                    _logger.LogWarning(
                        "CSP violation: directive={Directive} blocked={Blocked} document={Document} source={Source}:{Line}",
                        report.Directive, report.Blocked, report.Document, report.Source, report.Line);
                }
            }
            catch (JsonException)
            {
                // Malformed report: nothing useful to log, and the sender gets no detail back.
            }

            return NoContent();
        }

        private async Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken)
        {
            // Read at most MaxBodyBytes + 1 so an oversize body is detected without buffering it all.
            var buffer = new MemoryStream();
            var chunk = new byte[1024];
            int read;
            while (buffer.Length <= MaxBodyBytes &&
                   (read = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static IEnumerable<Violation> Extract(JsonElement root)
        {
            // application/csp-report: { "csp-report": { "document-uri", "effective-directive", ... } }
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var legacy)
                && legacy.ValueKind == JsonValueKind.Object)
            {
                yield return new Violation(
                    Clean(Str(legacy, "effective-directive") ?? Str(legacy, "violated-directive")),
                    ReduceToOrigin(Str(legacy, "blocked-uri")),
                    DocumentPath(Str(legacy, "document-uri")),
                    ReduceToOrigin(Str(legacy, "source-file"), keepPath: true),
                    Num(legacy, "line-number"));
                yield break;
            }

            // application/reports+json: [ { "type": "csp-violation", "body": { "effectiveDirective", ... } } ]
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !string.Equals(Str(item, "type"), "csp-violation", StringComparison.Ordinal)
                        || !item.TryGetProperty("body", out var b) || b.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    yield return new Violation(
                        Clean(Str(b, "effectiveDirective")),
                        ReduceToOrigin(Str(b, "blockedURL")),
                        DocumentPath(Str(b, "documentURL")),
                        ReduceToOrigin(Str(b, "sourceFile"), keepPath: true),
                        Num(b, "lineNumber"));
                }
            }
        }

        // ArcGIS 4.29 probes eval once per page load and falls back (findings-security 2.6): noise.
        private static bool IsKnownArcGisEval(Violation v) =>
            v.Directive == "script-src"
            && (v.Blocked is "eval" or "'unsafe-eval'")
            && v.Source.StartsWith("https://js.arcgis.com/4.29/", StringComparison.Ordinal);

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int Num(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;

        /// <summary>Strips control characters (log forging) and caps the length.</summary>
        private static string Clean(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "-";
            }
            var sb = new StringBuilder(Math.Min(s.Length, MaxFieldLength));
            foreach (var ch in s)
            {
                if (sb.Length >= MaxFieldLength)
                {
                    break;
                }
                sb.Append(char.IsControl(ch) ? '?' : ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Keywords such as "eval" or "inline" pass through; URLs shrink to scheme and host
        /// (plus the path when <paramref name="keepPath"/> is set; never the query or fragment).
        /// </summary>
        private static string ReduceToOrigin(string? s, bool keepPath = false)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "-";
            }
            if (Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
            {
                return Clean(keepPath ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}" : $"{uri.Scheme}://{uri.Host}");
            }
            // "data", "blob", "eval", "inline", "about", or "data:image/png;base64,..." -> scheme only.
            var colon = s.IndexOf(':');
            return Clean(colon > 0 ? s[..colon] : s);
        }

        private static string DocumentPath(string? s) =>
            Uri.TryCreate(s, UriKind.Absolute, out var uri) ? Clean(uri.AbsolutePath) : "-";

        private readonly record struct Violation(string Directive, string Blocked, string Document, string Source, int Line);
    }
}
