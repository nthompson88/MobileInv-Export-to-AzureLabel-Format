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
                Console.WriteLine("Drag a CSV onto this EXE, or run:");
                Console.WriteLine("  ItemNumberExtractor.exe \"C:\\path\\file.csv\"");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return 1;
            }

            var inputPath = args[0].Trim('"');
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"File not found: {inputPath}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return 2;
            }

            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrWhiteSpace(dir)) dir = Environment.CurrentDirectory;

            var outputPath = Path.Combine(
                dir,
                Path.GetFileNameWithoutExtension(inputPath) + "_item_numbers_single_line.txt"
            );

            var delimiter = DetectDelimiter(inputPath);

            int written = 0;
            bool firstRow = true;
            var preview = new List<string>(10);

            // Collect values first, then write one-line
            var items = new List<string>(capacity: 4096);

            using var parser = new TextFieldParser(inputPath, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(delimiter);

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length == 0) continue;

                var raw = Clean(fields[0]);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (firstRow)
                {
                    firstRow = false;
                    if (!(DigitsOnly.IsMatch(raw) || Scientific.IsMatch(raw)))
                        continue; // skip header
                }

                if (Scientific.IsMatch(raw))
                    raw = ExpandScientific(raw);

                // Remove ALL whitespace chars just in case
                raw = RemoveAllWhitespace(raw);

                if (!DigitsOnly.IsMatch(raw))
                    continue;

                items.Add(raw);
                written++;

                if (preview.Count < 10) preview.Add(raw);
            }

            // Write ONE LINE, comma-separated (no newlines)
            var oneLine = string.Join(",", items);

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.Write(oneLine);
                writer.Flush();
                fs.Flush(true);
            }

            Console.WriteLine($"Wrote {written} item numbers to:");
            Console.WriteLine(outputPath);

            if (written > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Preview:");
                foreach (var p in preview) Console.WriteLine(p);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex);
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
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
        => (s ?? "")
            .Replace("\u00A0", "")
            .Trim()
            .Trim('"')
            .Trim('\'')
            .Trim();

    static string RemoveAllWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsWhiteSpace(ch)) sb.Append(ch);
        return sb.ToString();
    }

    static string ExpandScientific(string s)
    {
        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0", CultureInfo.InvariantCulture);
        return s;
    }
}
