using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Swoq.Infra;

namespace MapGeneratorBenchmark;

public static class Benchmarks
{
    [Benchmark]
    public static void GenerateAll()
    {
        for (var l = 0; l <= MapGenerator.MaxLevel; l++)
        {
            MapGenerator.Generate(l);
        }
    }

    [Benchmark]
    public static void GenerateLevel1()
    {
        MapGenerator.Generate(1);
    }

    [Benchmark]
    public static void GenerateLevel4()
    {
        MapGenerator.Generate(4);
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "all")
        {
            Benchmarks.GenerateAll();
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
