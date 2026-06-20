using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Built-in spreadsheet functions registry helper.
/// </summary>
public static class SpreadsheetFunctions
{
    public static void RegisterMathFunctions(FunctionRegistry registry)
    {
        registry.Register("SUM", (ctx, args) =>
        {
            var values = FlattenArgs(args);
            return values.Sum();
        });

        registry.Register("AVERAGE", (ctx, args) =>
        {
            var values = FlattenArgs(args);
            return values.Count == 0 ? 0 : values.Average();
        });

        registry.Register("COUNT", (ctx, args) =>
        {
            var values = FlattenArgs(args);
            return (double)values.Count;
        });

        registry.Register("MIN", (ctx, args) =>
        {
            var values = FlattenArgs(args);
            return values.Count == 0 ? 0 : values.Min();
        });

        registry.Register("MAX", (ctx, args) =>
        {
            var values = FlattenArgs(args);
            return values.Count == 0 ? 0 : values.Max();
        });

        registry.Register("ABS", (ctx, args) =>
        {
            var val = ToDouble(args.FirstOrDefault());
            return Math.Abs(val);
        });

        registry.Register("ROUND", (ctx, args) =>
        {
            var number = ToDouble(args.FirstOrDefault());
            var digits = args.Count > 1 ? (int)ToDouble(args[1]) : 0;
            return Math.Round(number, digits, MidpointRounding.AwayFromZero);
        });

        registry.Register("ROUNDDOWN", (ctx, args) =>
        {
            var number = ToDouble(args.FirstOrDefault());
            var digits = args.Count > 1 ? (int)ToDouble(args[1]) : 0;
            var multiplier = Math.Pow(10, digits);
            return Math.Floor(number * multiplier) / multiplier;
        });

        registry.Register("ROUNDUP", (ctx, args) =>
        {
            var number = ToDouble(args.FirstOrDefault());
            var digits = args.Count > 1 ? (int)ToDouble(args[1]) : 0;
            var multiplier = Math.Pow(10, digits);
            return Math.Ceiling(number * multiplier) / multiplier;
        });

        registry.Register("MOD", (ctx, args) =>
        {
            var number = ToDouble(args.FirstOrDefault());
            var divisor = args.Count > 1 ? ToDouble(args[1]) : 1;
            return divisor == 0 ? new FormulaError("#DIV/0!") : number % divisor;
        });

        registry.Register("POWER", (ctx, args) =>
        {
            var number = ToDouble(args.FirstOrDefault());
            var exponent = args.Count > 1 ? ToDouble(args[1]) : 1;
            return Math.Pow(number, exponent);
        });

        registry.Register("SQRT", (ctx, args) =>
        {
            var val = ToDouble(args.FirstOrDefault());
            return Math.Sqrt(val);
        });

        registry.Register("PI", (ctx, args) => Math.PI);

        registry.Register("RAND", (ctx, args) => new Random().NextDouble());

        registry.Register("RANDBETWEEN", (ctx, args) =>
        {
            var bottom = (int)ToDouble(args.FirstOrDefault());
            var top = args.Count > 1 ? (int)ToDouble(args[1]) : bottom;
            return new Random().Next(bottom, top + 1);
        });
    }

    public static void RegisterTextFunctions(FunctionRegistry registry)
    {
        registry.Register("CONCATENATE", (ctx, args) => string.Concat(args.Select(a => a?.ToString() ?? string.Empty)));

        registry.Register("LEFT", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var numChars = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            if (numChars <= 0) return string.Empty;
            if (numChars >= text.Length) return text;
            return text[..numChars];
        });

        registry.Register("RIGHT", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var numChars = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            if (numChars <= 0) return string.Empty;
            if (numChars >= text.Length) return text;
            return text[^numChars..];
        });

        registry.Register("MID", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var start = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            var numChars = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            if (start <= 0 || numChars <= 0 || start > text.Length) return string.Empty;
            var startIndex = start - 1; // Excel uses 1-based indexing
            var length = Math.Min(numChars, text.Length - startIndex);
            return text.Substring(startIndex, length);
        });

        registry.Register("LEN", (ctx, args) => (double)(args.FirstOrDefault()?.ToString() ?? string.Empty).Length);

        registry.Register("TRIM", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            // Excel TRIM removes leading/trailing spaces and reduces multiple spaces to single
            return Regex.Replace(text.Trim(), @"\s+", " ");
        });

        registry.Register("UPPER", (ctx, args) => (args.FirstOrDefault()?.ToString() ?? string.Empty).ToUpperInvariant());
        registry.Register("LOWER", (ctx, args) => (args.FirstOrDefault()?.ToString() ?? string.Empty).ToLowerInvariant());

        registry.Register("PROPER", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder();
            bool newWord = true;
            foreach (var ch in text)
            {
                if (!char.IsLetter(ch))
                {
                    newWord = true;
                    sb.Append(ch);
                }
                else if (newWord)
                {
                    sb.Append(char.ToUpperInvariant(ch));
                    newWord = false;
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }
            return sb.ToString();
        });

        registry.Register("TEXT", (ctx, args) =>
        {
            var value = args.FirstOrDefault();
            var format = args.Count > 1 ? args[1]?.ToString() ?? "General" : "General";
            return SpreadsheetNumberFormatter.Format(value, format);
        });

        registry.Register("VALUE", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return new FormulaError("#VALUE!");
        });

        registry.Register("FIND", (ctx, args) =>
        {
            var findText = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var withinText = args.Count > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
            var startNum = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            if (string.IsNullOrEmpty(findText) || string.IsNullOrEmpty(withinText) || startNum <= 0)
                return new FormulaError("#VALUE!");
            var idx = withinText.IndexOf(findText, startNum - 1, StringComparison.Ordinal);
            if (idx < 0) return new FormulaError("#VALUE!");
            return (double)(idx + 1);
        });

        registry.Register("SEARCH", (ctx, args) =>
        {
            var findText = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var withinText = args.Count > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
            var startNum = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            if (string.IsNullOrEmpty(findText) || string.IsNullOrEmpty(withinText) || startNum <= 0)
                return new FormulaError("#VALUE!");
            var idx = withinText.IndexOf(findText, startNum - 1, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return new FormulaError("#VALUE!");
            return (double)(idx + 1);
        });

        registry.Register("SUBSTITUTE", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var oldText = args.Count > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
            var newText = args.Count > 2 ? args[2]?.ToString() ?? string.Empty : string.Empty;
            var instanceNum = args.Count > 3 ? (int?)ToDouble(args[3]) : null;

            if (string.IsNullOrEmpty(oldText)) return text;
            if (instanceNum.HasValue && instanceNum.Value > 0)
            {
                var occurrences = 0;
                var sb = new StringBuilder();
                var i = 0;
                while (i < text.Length)
                {
                    if (text.AsSpan(i).StartsWith(oldText))
                    {
                        occurrences++;
                        if (occurrences == instanceNum.Value)
                        {
                            sb.Append(newText);
                            i += oldText.Length;
                            continue;
                        }
                    }
                    sb.Append(text[i]);
                    i++;
                }
                return sb.ToString();
            }
            return text.Replace(oldText, newText);
        });

        registry.Register("REPT", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            var number = (int)ToDouble(args.Count > 1 ? args[1] : 0);
            if (number <= 0) return string.Empty;
            return new StringBuilder(text.Length * number).Insert(0, text, number).ToString();
        });
    }

    public static void RegisterLogicalFunctions(FunctionRegistry registry)
    {
        registry.Register("IF", (ctx, args) =>
        {
            var condition = ToBool(args.FirstOrDefault());
            var trueValue = args.Count > 1 ? args[1] : null;
            var falseValue = args.Count > 2 ? args[2] : null;
            return condition ? trueValue : falseValue;
        });

        registry.Register("AND", (ctx, args) =>
        {
            if (args.Count == 0) return new FormulaError("#VALUE!");
            var flat = FlattenArgs(args);
            return flat.All(v => v != 0);
        });

        registry.Register("OR", (ctx, args) =>
        {
            if (args.Count == 0) return new FormulaError("#VALUE!");
            var flat = FlattenArgs(args);
            return flat.Any(v => v != 0);
        });

        registry.Register("NOT", (ctx, args) => !ToBool(args.FirstOrDefault()));

        registry.Register("TRUE", (ctx, args) => true);
        registry.Register("FALSE", (ctx, args) => false);

        registry.Register("IFERROR", (ctx, args) =>
        {
            if (args.Count == 0) return null;
            if (args[0] is FormulaError)
                return args.Count > 1 ? args[1] : null;
            return args[0];
        });

        registry.Register("ISBLANK", (ctx, args) => args.FirstOrDefault() is null);
        registry.Register("ISNUMBER", (ctx, args) => args.FirstOrDefault() is double or int or long or decimal or float);
        registry.Register("ISTEXT", (ctx, args) => args.FirstOrDefault() is string);
        registry.Register("ISERROR", (ctx, args) => args.FirstOrDefault() is FormulaError);
        registry.Register("ISLOGICAL", (ctx, args) => args.FirstOrDefault() is bool);
        registry.Register("ISEVEN", (ctx, args) => (int)ToDouble(args.FirstOrDefault()) % 2 == 0);
        registry.Register("ISODD", (ctx, args) => (int)ToDouble(args.FirstOrDefault()) % 2 != 0);
    }

    public static void RegisterDateTimeFunctions(FunctionRegistry registry)
    {
        // Excel base date: 1900-01-00 (with 1900 leap year bug compatibility)
        var excelBase = new DateTime(1899, 12, 30);

        double ToSerial(DateTime dt) => (dt - excelBase).TotalDays;
        DateTime FromSerial(double serial) => excelBase.AddDays(serial);

        registry.Register("DATE", (ctx, args) =>
        {
            var year = (int)ToDouble(args.FirstOrDefault());
            var month = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            var day = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            try { return ToSerial(new DateTime(year, month, day)); }
            catch { return new FormulaError("#VALUE!"); }
        });

        registry.Register("TIME", (ctx, args) =>
        {
            var hour = ToDouble(args.FirstOrDefault());
            var minute = args.Count > 1 ? ToDouble(args[1]) : 0;
            var second = args.Count > 2 ? ToDouble(args[2]) : 0;
            return (hour + minute / 60 + second / 3600) / 24;
        });

        registry.Register("NOW", (ctx, args) => ToSerial(DateTime.Now));
        registry.Register("TODAY", (ctx, args) => Math.Floor(ToSerial(DateTime.Today)));

        registry.Register("YEAR", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Year);
        registry.Register("MONTH", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Month);
        registry.Register("DAY", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Day);
        registry.Register("HOUR", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Hour);
        registry.Register("MINUTE", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Minute);
        registry.Register("SECOND", (ctx, args) => (double)FromSerial(ToDouble(args.FirstOrDefault())).Second);

        registry.Register("WEEKDAY", (ctx, args) =>
        {
            var serial = ToDouble(args.FirstOrDefault());
            var returnType = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            var dow = (int)FromSerial(serial).DayOfWeek; // Sunday=0
            return returnType switch
            {
                1 => dow + 1, // 1=Sunday
                2 => dow == 0 ? 7 : dow, // 1=Monday
                3 => dow, // 0=Monday
                _ => dow + 1
            };
        });

        registry.Register("WEEKNUM", (ctx, args) =>
        {
            var serial = ToDouble(args.FirstOrDefault());
            var dt = FromSerial(serial);
            var cal = CultureInfo.InvariantCulture.Calendar;
            return (double)cal.GetWeekOfYear(dt, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        });

        registry.Register("DAYS", (ctx, args) =>
        {
            var end = ToDouble(args.FirstOrDefault());
            var start = args.Count > 1 ? ToDouble(args[1]) : 0;
            return end - start;
        });

        registry.Register("EDATE", (ctx, args) =>
        {
            var serial = ToDouble(args.FirstOrDefault());
            var months = (int)ToDouble(args.Count > 1 ? args[1] : 0);
            return ToSerial(FromSerial(serial).AddMonths(months));
        });

        registry.Register("EOMONTH", (ctx, args) =>
        {
            var serial = ToDouble(args.FirstOrDefault());
            var months = (int)ToDouble(args.Count > 1 ? args[1] : 0);
            var dt = FromSerial(serial).AddMonths(months);
            return ToSerial(new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)));
        });

        registry.Register("DATEDIF", (ctx, args) =>
        {
            var start = FromSerial(ToDouble(args.FirstOrDefault()));
            var end = FromSerial(args.Count > 1 ? ToDouble(args[1]) : 0);
            var unit = args.Count > 2 ? args[2]?.ToString()?.ToUpperInvariant() ?? "D" : "D";
            return unit switch
            {
                "D" => (end - start).TotalDays,
                "M" => ((end.Year - start.Year) * 12) + end.Month - start.Month,
                "Y" => end.Year - start.Year,
                _ => (end - start).TotalDays
            };
        });

        registry.Register("DATEVALUE", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return ToSerial(dt.Date);
            return new FormulaError("#VALUE!");
        });

        registry.Register("TIMEVALUE", (ctx, args) =>
        {
            var text = args.FirstOrDefault()?.ToString() ?? string.Empty;
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var ts))
                return ts.TotalDays;
            return new FormulaError("#VALUE!");
        });
    }

    public static void RegisterLookupFunctions(FunctionRegistry registry)
    {
        registry.Register("VLOOKUP", (ctx, args) =>
        {
            var lookupValue = args.FirstOrDefault();
            var rangeRef = args.Count > 1 ? args[1]?.ToString() : null;
            var colIndex = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            var rangeLookup = args.Count > 3 ? ToBool(args[3]) : true;

            if (string.IsNullOrEmpty(rangeRef) || colIndex < 1)
                return new FormulaError("#REF!");

            var cells = GetRangeValues(ctx, rangeRef);
            if (cells.Count == 0) return new FormulaError("#N/A");

            var cols = GetRangeColumns(rangeRef);
            if (colIndex > cols) return new FormulaError("#REF!");

            var lookupNum = ToDouble(lookupValue);
            var lookupText = lookupValue?.ToString() ?? string.Empty;

            for (var i = 0; i < cells.Count; i += cols)
            {
                var firstColValue = cells[i];
                var match = false;
                if (rangeLookup)
                {
                    // Approximate match (requires sorted data)
                    var firstColNum = ToDouble(firstColValue);
                    if (lookupNum >= firstColNum)
                    {
                        match = true;
                        // Check if next row is greater
                        if (i + cols < cells.Count)
                        {
                            var nextNum = ToDouble(cells[i + cols]);
                            if (lookupNum < nextNum) match = true;
                            else continue;
                        }
                    }
                }
                else
                {
                    match = firstColValue?.ToString() == lookupText;
                }

                if (match)
                {
                    var resultIndex = i + colIndex - 1;
                    return resultIndex < cells.Count ? cells[resultIndex] : new FormulaError("#REF!");
                }
            }
            return new FormulaError("#N/A");
        });

        registry.Register("HLOOKUP", (ctx, args) =>
        {
            var lookupValue = args.FirstOrDefault();
            var rangeRef = args.Count > 1 ? args[1]?.ToString() : null;
            var rowIndex = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            var rangeLookup = args.Count > 3 ? ToBool(args[3]) : true;

            if (string.IsNullOrEmpty(rangeRef) || rowIndex < 1)
                return new FormulaError("#REF!");

            var cells = GetRangeValues(ctx, rangeRef);
            if (cells.Count == 0) return new FormulaError("#N/A");

            var cols = GetRangeColumns(rangeRef);
            var rows = cells.Count / cols;
            if (rowIndex > rows) return new FormulaError("#REF!");

            var lookupNum = ToDouble(lookupValue);
            var lookupText = lookupValue?.ToString() ?? string.Empty;

            for (var c = 0; c < cols; c++)
            {
                var firstRowValue = cells[c];
                var match = false;
                if (rangeLookup)
                {
                    var firstRowNum = ToDouble(firstRowValue);
                    if (lookupNum >= firstRowNum)
                    {
                        match = true;
                        if (c + 1 < cols)
                        {
                            var nextNum = ToDouble(cells[c + 1]);
                            if (lookupNum < nextNum) match = true;
                            else continue;
                        }
                    }
                }
                else
                {
                    match = firstRowValue?.ToString() == lookupText;
                }

                if (match)
                {
                    var resultIndex = (rowIndex - 1) * cols + c;
                    return resultIndex < cells.Count ? cells[resultIndex] : new FormulaError("#REF!");
                }
            }
            return new FormulaError("#N/A");
        });

        registry.Register("INDEX", (ctx, args) =>
        {
            var rangeRef = args.FirstOrDefault()?.ToString();
            var rowNum = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            var colNum = args.Count > 2 ? (int)ToDouble(args[2]) : 1;

            if (string.IsNullOrEmpty(rangeRef))
                return new FormulaError("#REF!");

            var cells = GetRangeValues(ctx, rangeRef);
            var cols = GetRangeColumns(rangeRef);

            var index = (rowNum - 1) * cols + (colNum - 1);
            if (index < 0 || index >= cells.Count)
                return new FormulaError("#REF!");
            return cells[index];
        });

        registry.Register("MATCH", (ctx, args) =>
        {
            var lookupValue = args.FirstOrDefault();
            var rangeRef = args.Count > 1 ? args[1]?.ToString() : null;
            var matchType = args.Count > 2 ? (int)ToDouble(args[2]) : 1;

            if (string.IsNullOrEmpty(rangeRef))
                return new FormulaError("#N/A");

            var cells = GetRangeValues(ctx, rangeRef);
            if (cells.Count == 0) return new FormulaError("#N/A");

            var lookupNum = ToDouble(lookupValue);
            var lookupText = lookupValue?.ToString() ?? string.Empty;

            if (matchType == 0)
            {
                for (var i = 0; i < cells.Count; i++)
                    if (cells[i]?.ToString() == lookupText)
                        return (double)(i + 1);
                return new FormulaError("#N/A");
            }

            // Approximate match (1 = less than, -1 = greater than)
            for (var i = 0; i < cells.Count; i++)
            {
                var val = ToDouble(cells[i]);
                if (matchType > 0 && val <= lookupNum)
                {
                    if (i + 1 >= cells.Count || ToDouble(cells[i + 1]) > lookupNum)
                        return (double)(i + 1);
                }
                else if (matchType < 0 && val >= lookupNum)
                {
                    if (i + 1 >= cells.Count || ToDouble(cells[i + 1]) < lookupNum)
                        return (double)(i + 1);
                }
            }
            return new FormulaError("#N/A");
        });
        registry.Register("CHOOSE", (ctx, args) =>
        {
            var index = (int)ToDouble(args.FirstOrDefault());
            if (index < 1 || index >= args.Count)
                return new FormulaError("#VALUE!");
            return args[index];
        });
        registry.Register("OFFSET", (ctx, args) => new FormulaError("#REF!"));
        registry.Register("INDIRECT", (ctx, args) =>
        {
            var refText = args.FirstOrDefault()?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(refText))
                return new FormulaError("#REF!");
            return ctx.ResolveCellRef(refText);
        });
        registry.Register("ROW", (ctx, args) =>
        {
            if (args.FirstOrDefault() is string s)
            {
                var numbers = new string(s.SkipWhile(char.IsLetter).ToArray());
                if (int.TryParse(numbers, out var row))
                    return (double)row;
            }
            return 1.0;
        });
        registry.Register("COLUMN", (ctx, args) =>
        {
            if (args.FirstOrDefault() is string s)
            {
                var letters = new string(s.TakeWhile(char.IsLetter).ToArray());
                return (double)SpreadsheetRange.ColumnLettersToIndex(letters) + 1;
            }
            return 1.0;
        });
        registry.Register("ROWS", (ctx, args) =>
        {
            if (args.FirstOrDefault() is string s && s.Contains(':'))
            {
                var parts = s.Split(':');
                var r1 = int.Parse(new string(parts[0].SkipWhile(char.IsLetter).ToArray()));
                var r2 = int.Parse(new string(parts[1].SkipWhile(char.IsLetter).ToArray()));
                return (double)(Math.Abs(r2 - r1) + 1);
            }
            return 1.0;
        });
        registry.Register("COLUMNS", (ctx, args) =>
        {
            if (args.FirstOrDefault() is string s && s.Contains(':'))
            {
                var parts = s.Split(':');
                var c1 = SpreadsheetRange.ColumnLettersToIndex(new string(parts[0].TakeWhile(char.IsLetter).ToArray()));
                var c2 = SpreadsheetRange.ColumnLettersToIndex(new string(parts[1].TakeWhile(char.IsLetter).ToArray()));
                return (double)(Math.Abs(c2 - c1) + 1);
            }
            return 1.0;
        });
        registry.Register("ADDRESS", (ctx, args) =>
        {
            var row = (int)ToDouble(args.FirstOrDefault());
            var col = args.Count > 1 ? (int)ToDouble(args[1]) : 1;
            var absNum = args.Count > 2 ? (int)ToDouble(args[2]) : 1;
            var a1Style = args.Count > 3 ? ToBool(args[3]) : true;
            var sheetText = args.Count > 4 ? args[4]?.ToString() : null;

            if (!a1Style) return new FormulaError("#VALUE!"); // R1C1 not supported

            var colLetters = SpreadsheetRange.ColumnIndexToLetters(col - 1);
            var absRow = absNum == 1 || absNum == 2 ? "$" : "";
            var absCol = absNum == 1 || absNum == 3 ? "$" : "";
            var address = $"{absCol}{colLetters}{absRow}{row}";
            return string.IsNullOrEmpty(sheetText) ? address : $"'{sheetText}'!{address}";
        });
        registry.Register("AREAS", (ctx, args) => 1.0);
    }

    private static List<double> FlattenArgs(List<object?> args)
    {
        var result = new List<double>();
        foreach (var arg in args)
        {
            if (arg is List<object?> list)
            {
                result.AddRange(list.Select(ToDouble));
            }
            else
            {
                result.Add(ToDouble(arg));
            }
        }
        return result;
    }

    private static double ToDouble(object? value)
    {
        if (value is null) return 0;
        if (value is FormulaError) return 0;
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal dec) return (double)dec;
        if (value is float f) return f;
        if (value is bool b) return b ? 1 : 0;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (value is List<object?> list)
        {
            return list.Select(ToDouble).Sum();
        }
        return 0;
    }

    private static bool ToBool(object? value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        if (value is double d) return d != 0;
        if (value is int i) return i != 0;
        if (value is string s) return !string.IsNullOrEmpty(s);
        return true;
    }

    private static List<object?> GetRangeValues(FormulaContext ctx, string rangeRef)
    {
        var result = new List<object?>();
        try
        {
            var range = SpreadsheetRange.Parse(rangeRef);
            foreach (var cellRef in range.CellRefs)
            {
                result.Add(ctx.ResolveCellRef(cellRef));
            }
        }
        catch { /* ignore invalid range */ }
        return result;
    }

    private static int GetRangeColumns(string rangeRef)
    {
        try
        {
            var range = SpreadsheetRange.Parse(rangeRef);
            return range.EndCol - range.StartCol + 1;
        }
        catch { return 1; }
    }
}
