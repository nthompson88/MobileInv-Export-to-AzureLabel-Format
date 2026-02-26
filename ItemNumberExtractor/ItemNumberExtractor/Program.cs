using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class Program
{
    // Digits only (keeps leading zeros)
    static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

    // Scientific notation like 1.81091E+11
    static readonly Regex Scientific = new(@"^[+-]?\d+(\.\d+)?[eE][+-]?\d+$", RegexOptions.Compiled);

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  Drag-and-drop a .csv onto this .exe");
            Console.WriteLine("  OR run: ItemNumberExtractor.exe \"path\\to\\file.csv\"");
            return 1;
        }

        var inputPath = args[0].Trim('"');

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"File not found: {inputPath}");
            return 2;
        }

        // Output next to input file
        var outPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? ".",
            Path.GetFileNameWithoutExtension(inputPath) + "_item_numbers.txt"
        );

        int extracted = 0;

        // Read as raw text. This avoids Excel interpretation.
        // UTF-8 with BOM handled fine by StreamReader default; we also trim NBSP.
        using var reader = new StreamReader(inputPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var writer = new StreamWriter(outPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        string? line;
        bool firstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            // Skip header line (most CSV exports have one)
            if (firstLine)
            {
                firstLine = false;
                continue;
            }

            // Basic CSV split: this assumes your file is simple (no embedded commas inside quotes)
            // If you expect quoted commas, tell me and I’ll swap in a real CSV parser.
            var cells = line.Split(',');

            foreach (var raw in cells)
            {
                var cell = Clean(raw);

                if (cell.Length == 0) continue;

                if (DigitsOnly.IsMatch(cell))
                {
                    writer.WriteLine(cell);
                    extracted++;
                }
                else if (Scientific.IsMatch(cell))
                {
                    var expanded = ExpandScientific(cell);
                    // Only write if expansion became digits
                    if (DigitsOnly.IsMatch(expanded))
                    {
                        writer.WriteLine(expanded);
                        extracted++;
                    }
                }
            }
        }

        Console.WriteLine($"Done. Extracted {extracted} item numbers to:");
        Console.WriteLine(outPath);
        return 0;
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // trim whitespace + non-breaking space
        return s.Replace("\u00A0", "").Trim().Trim('"').Trim('\'').Trim();
    }

    static string ExpandScientific(string s)
    {
        // Use decimal to expand exponent precisely (as long as the source actually contains the value).
        // If Excel already damaged the number before export, no code can recover missing digits.
        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            // "0" format forces no exponent and no decimals
            return d.ToString("0", CultureInfo.InvariantCulture);
        }
        return s;
    }
}
