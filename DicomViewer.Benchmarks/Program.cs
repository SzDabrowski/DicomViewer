using BenchmarkDotNet.Running;

namespace DicomViewer.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks, or pass --filter to pick specific ones:
        //   dotnet run -c Release -- --filter "*RgbaBuffer*"
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
