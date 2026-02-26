using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

static class Program
{
    static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);
    static readonly Regex Scientific = new(@"^[+-]?\d+(\.\d+)?[eE][+-]?\d+$", RegexOptions.Compiled);

    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag a CSV onto this EXE, or run: ItemNumberExtractor.exe \"path\\file.csv\"");
                Console.ReadKey();
                return 1;
            }

            var inputPath = args[0].Trim('"');
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"File not found: {inputPath}");
                Console.ReadKey();
                return 2;
            }

            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrWhiteSpace(dir)) dir = Environment.CurrentDirectory;

            var outputPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(inputPath) + "_item_numbers.txt");

            // Detect delimiter (comma/semicolon/tab) from first non-empty line
            var delimiter = DetectDelimiter(inputPath);

            int written = 0;
            bool firstRow = true;

            using var parser = new TextFieldParser(inputPath, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(delimiter);

            using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length == 0) continue;

                // Only use FIRST column (item number). This is the key fix.
                var raw = Clean(fields[0]);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (firstRow)
                {
                    firstRow = false;
                    // Skip header only if it doesn't look like an item number
                    if (!(DigitsOnly.IsMatch(raw) || Scientific.IsMatch(raw)))
                        continue;
                }

                if (Scientific.IsMatch(raw))
                    raw = ExpandScientific(raw);

                writer.WriteLine(raw);
                written++;
            }

            Console.WriteLine($"Wrote {written} item numbers to:");
            Console.WriteLine(outputPath);
            Console.ReadKey();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex);
            Console.ReadKey();
            return 99;
        }
    }

    static string DetectDelimiter(string path)
    {
        using var sr = new StreamReader(path, Encoding.UTF8, true);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            int comma = line.Count(c => c == ',');
            int semi  = line.Count(c => c == ';');
            int tab   = line.Count(c => c == '\t');

            if (tab >= semi && tab >= comma && tab > 0) return "\t";
            if (semi >= comma && semi > 0) return ";";
            return ",";
        }
        return ",";
    }

    static string Clean(string s)
        => (s ?? "").Replace("\u00A0", "").Trim().Trim('"').Trim('\'');

    static string ExpandScientific(string s)
    {
        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0", CultureInfo.InvariantCulture);
        return s;
    }
}
