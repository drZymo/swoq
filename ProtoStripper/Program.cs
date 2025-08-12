using Swoq.Interface;

namespace Swoq.ProtoStripper;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} <input_path> <output_path> [<max_level>]");
            return;
        }

        var inputPath = args[0];
        var outputPath = args[1];
        var maxLevel = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0;

        var contents = ProtoReader.ReadProtoFileForLevel(inputPath, maxLevel);

        File.WriteAllText(outputPath, contents);
    }
}
