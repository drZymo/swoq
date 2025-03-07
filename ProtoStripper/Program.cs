using Swoq.Interface;

namespace Swoq.ProtoStripper;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} <input_path> <output_path>");
            return;
        }

        var inputPath = args[0];
        var outputPath = args[1];

        var contents = ProtoReader.ReadProtoFileForLevel(inputPath, 0);

        File.WriteAllText(outputPath, contents);
    }
}
