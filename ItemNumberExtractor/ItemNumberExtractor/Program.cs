using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

static class Program
{
    static readonly Regex DigitsOnly  = new(@"^\d+$", RegexOptions.Compiled);
    static readonly Regex Scientific  = new(@"^[+-]?\d+(\.\d+)?[eE][+-]?\d+$", RegexOptions.Compiled);

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
            if (string.IsNullOrWhiteSpace(dir))
                dir = Environment.CurrentDirectory;

            var outputPath = Path.Combine(
                dir,
                Path.GetFileNameWithoutExtension(inputPath) + "_item_numbers.txt"
            );

            var delimiter = DetectDelimiter(inputPath);

            int written = 0;
            bool firstRow = true;
            var preview = new List<string>(capacity: 10);

            using var parser = new TextFieldParser(inputPath, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(delimiter);

            // Use FileStream so we can force flush and allow reading while open.
            using var fs = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read
            );

            using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length == 0) continue;

                // ONLY FIRST COLUMN: this prevents the "1" column from ever being written.
                var raw = Clean(fields[0]);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // Skip header only if first cell is not numeric/scientific
                if (firstRow)
                {
                    firstRow = false;
                    if (!(DigitsOnly.IsMatch(raw) || Scientific.IsMatch(raw)))
                        continue;
                }

                if (Scientific.IsMatch(raw))
                    raw = ExpandScientific(raw);

                writer.WriteLine(raw);
                written++;

                if (preview.Count < 10)
                    preview.Add(raw);
            }

            // Force everything to disk before we report success.
            writer.Flush();
            fs.Flush(true);

            Console.WriteLine($"Wrote {written} item numbers to:");
            Console.WriteLine(outputPath);

            if (written > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Preview:");
                foreach (var p in preview)
                    Console.WriteLine(p);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("NOTE: 0 items written. This usually means column 0 is blank or the file is not delimited as expected.");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex.ToString());
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
            .Replace("\u00A0", "")   // non-breaking space
            .Trim()
            .Trim('"')
            .Trim('\'')
            .Trim();

    static string ExpandScientific(string s)
    {
        // Expands values like 1.81091E+11 into 181091000000 (as dictated by the exponent).
        // If the original number was damaged by Excel before export, no code can recover missing digits.
        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0", CultureInfo.InvariantCulture);

        return s;
    }
}
