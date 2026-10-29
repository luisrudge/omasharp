#if OMASHARP_NATIVE && OMASHARP_DEV
using System.Runtime.InteropServices;

namespace Omasharp;

static unsafe class DevMain
{
    static int Main()
    {
        if (!NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), "omasharp_host_run", out var run))
        {
            Console.Error.WriteLine("Start this app with `dotnet run` (it needs the Omasharp host).");
            return 1;
        }
        return ((delegate* unmanaged<Api*, int>)run)(NativeExports.CreateApi());
    }
}
#endif
