using System.Runtime.CompilerServices;

// Mirrors Portfolio.Services/AssemblyInfo.cs. Lets the test project reach helpers that are
// implementation detail of the web host and have no business being public (for example
// IndexModel.BuildResumeMeta, the Startup/* classes and the rate-limiting policies).
[assembly: InternalsVisibleTo("Portfolio.Tests")]
