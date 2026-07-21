using System.Runtime.CompilerServices;

// Mirrors Portfolio.Services/AssemblyInfo.cs. Lets the test project reach helpers
// that are implementation detail of a page or controller and have no business
// being public — currently IndexModel.BuildResumeMeta.
[assembly: InternalsVisibleTo("Portfolio.Tests")]
