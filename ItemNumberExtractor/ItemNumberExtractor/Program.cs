using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class Program
{
    static readonly Regex Scientific = new(@"^[+-]?\d+(\.\d+)?[eE][+-]?\d+$");

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Drag a CSV onto this EXE.");
            Console.ReadKey();
            return 1;
        }

        var inputPath = args[0];

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("File not found.");
            Console.ReadKey();
            return 1;
        }

        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath)!,
            Path.GetFileNameWithoutExtension(inputPath) + "_item_numbers.txt"
        );

        using var reader = new StreamReader(inputPath, Encoding.UTF8, true);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));

        bool firstLine = true;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Skip header
            if (firstLine)
            {
                firstLine = false;
                continue;
            }

            var columns = line.Split(',');

            if (columns.Length == 0) continue;

            var item = columns[0].Trim().Replace("\u00A0", "");

            if (Scientific.IsMatch(item))
            {
                if (decimal.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    item = d.ToString("0", CultureInfo.InvariantCulture);
            }

            writer.WriteLine(item);
        }

        Console.WriteLine("Done.");
        Console.WriteLine(outputPath);
        Console.ReadKey();
        return 0;
    }
}
