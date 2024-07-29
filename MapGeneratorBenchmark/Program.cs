using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Swoq.Infra;

namespace MapGeneratorBenchmark;

public class Benchmarks
{
    private readonly MapGenerator generator = new(64, 64);

    [Benchmark]
    public void GenerateAll()
    {
        for (var l = 0; l < 20; l++)
        {
            generator.Generate(l);
        }
    }

    [Benchmark]
    public void GenerateLevel1()
    {
        generator.Generate(1);
    }

    [Benchmark]
    public void GenerateLevel4()
    {
        generator.Generate(4);
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
