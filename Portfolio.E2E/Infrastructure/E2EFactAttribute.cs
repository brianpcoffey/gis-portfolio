using System.Runtime.CompilerServices;

namespace Portfolio.E2E.Infrastructure;

/// <summary>The switch that turns the browser tests on.</summary>
/// <remarks>
/// A plain <c>dotnet test</c> at the repo root must not need Chromium, so the browser tests report
/// Skipped unless <c>PORTFOLIO_E2E=1</c> (the same convention as the native-gated tests). CI's <c>e2e</c>
/// job sets it after installing the browser.
/// </remarks>
public static class E2EGate
{
    /// <summary>Name of the environment variable that enables the browser tests.</summary>
    public const string Variable = "PORTFOLIO_E2E";

    /// <summary>Shown as the skip reason in the test report.</summary>
    public const string SkipReason = "Browser E2E tests run only when PORTFOLIO_E2E=1 (and Chromium is installed).";

    /// <summary>True when <c>PORTFOLIO_E2E=1</c> was set for this process.</summary>
    public static bool Enabled { get; } = Environment.GetEnvironmentVariable(Variable) == "1";
}

/// <summary>A <see cref="FactAttribute"/> that skips unless <see cref="E2EGate.Enabled"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!E2EGate.Enabled)
            Skip = E2EGate.SkipReason;
    }
}

/// <summary>A <see cref="TheoryAttribute"/> that skips unless <see cref="E2EGate.Enabled"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class E2ETheoryAttribute : TheoryAttribute
{
    public E2ETheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!E2EGate.Enabled)
            Skip = E2EGate.SkipReason;
    }
}
