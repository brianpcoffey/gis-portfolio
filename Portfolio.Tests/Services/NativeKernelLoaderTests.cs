using Portfolio.Services.Native;
using System.Runtime.InteropServices;

namespace Portfolio.Tests.Services
{
    public class NativeKernelLoaderTests
    {
        [Fact]
        public void TryLoad_UnknownKernel_ReturnsFalse()
        {
            Assert.False(NativeKernelLoader.TryLoad("no_such_kernel"));
        }

        public static TheoryData<Exception, bool> Classifications => new()
        {
            { new DllNotFoundException(), true },
            { new EntryPointNotFoundException(), true },
            { new BadImageFormatException(), true },
            { new SEHException(), true },
            { new InvalidOperationException(), true },
            { new ArgumentException(), false },
            { new NullReferenceException(), false },
            { new OutOfMemoryException(), false },
            { new Exception(), false },
        };

        [Theory]
        [MemberData(nameof(Classifications))]
        public void IsNativeInvocationException_TruthTable(Exception exception, bool expected)
        {
            Assert.Equal(expected, NativeKernelLoader.IsNativeInvocationException(exception));
        }
    }
}
