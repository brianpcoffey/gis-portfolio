using System.Runtime.CompilerServices;

// Mirrors Portfolio.Services/AssemblyInfo.cs. Lets the test project reach helpers that are
// implementation detail of the web host and have no business being public (for example
// IndexModel.BuildResumeMeta, the Startup/* classes and the rate-limiting policies).
[assembly: InternalsVisibleTo("Portfolio.Tests")]
// The browser E2E project boots the real host through WebApplicationFactory<Program>, and the generated
// top-level Program class is internal. This only widens the compile-time visibility of internals; it adds no
// runtime behaviour, and no E2E code is referenced from this assembly.
[assembly: InternalsVisibleTo("Portfolio.E2E")]
