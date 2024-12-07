using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Swoq.Infra;

namespace MapGeneratorBenchmark;

[InProcess]
public class Benchmarks
{
    [Benchmark]
    public void GenerateAll()
    {
        for (var l = 0; l <= MapGenerator.MaxLevel; l++)
        {
            MapGenerator.Generate(l);
        }
    }

    [Benchmark]
    public void GenerateLevel1()
    {
        MapGenerator.Generate(1);
    }

    [Benchmark]
    public void GenerateLevel4()
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
            new Benchmarks().GenerateAll();
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
