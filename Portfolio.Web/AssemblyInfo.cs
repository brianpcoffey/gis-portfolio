using System.Runtime.CompilerServices;

// Mirrors Portfolio.Services/AssemblyInfo.cs. Lets the test project reach helpers
// that are implementation detail of a page, filter or startup class and have no
// business being public (for example IndexModel.BuildResumeMeta and the Startup/* classes).
[assembly: InternalsVisibleTo("Portfolio.Tests")]
