namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Built-in formula function metadata used by spreadsheet editing UX helpers.
/// </summary>
public static class SpreadsheetFormulaFunctionCatalog
{
    /// <summary>All built-in spreadsheet functions exposed to formula autocomplete.</summary>
    public static IReadOnlyList<SpreadsheetFormulaFunctionMetadata> All { get; } =
    [
        Function("ABS", "ABS(number)", "Returns the absolute value.", "number"),
        Function("ADDRESS", "ADDRESS(row, column, [abs], [a1], [sheet])", "Builds a cell address from row and column numbers.", "row", "column", "abs", "a1", "sheet"),
        Function("AND", "AND(logical1, [logical2], ...)", "Returns TRUE when all conditions are TRUE.", "logical1", "logical2"),
        Function("AREAS", "AREAS(reference)", "Returns the number of referenced areas.", "reference"),
        Function("AVERAGE", "AVERAGE(number1, [number2], ...)", "Returns the arithmetic mean.", "number1", "number2"),
        Function("CHOOSE", "CHOOSE(index, value1, [value2], ...)", "Returns the value at the chosen index.", "index", "value1", "value2"),
        Function("COLUMN", "COLUMN([reference])", "Returns the column number for a reference.", "reference"),
        Function("COLUMNS", "COLUMNS(array)", "Returns the number of columns in an array or range.", "array"),
        Function("CONCATENATE", "CONCATENATE(text1, [text2], ...)", "Joins multiple text values together.", "text1", "text2"),
        Function("COUNT", "COUNT(value1, [value2], ...)", "Counts numeric values.", "value1", "value2"),
        Function("DATE", "DATE(year, month, day)", "Builds a serial date from year, month, and day.", "year", "month", "day"),
        Function("DATEDIF", "DATEDIF(start_date, end_date, unit)", "Returns the difference between two dates.", "start_date", "end_date", "unit"),
        Function("DATEVALUE", "DATEVALUE(text)", "Parses text into a date serial value.", "text"),
        Function("DAYS", "DAYS(end_date, start_date)", "Returns the number of days between two dates.", "end_date", "start_date"),
        Function("EDATE", "EDATE(start_date, months)", "Shifts a date by a number of months.", "start_date", "months"),
        Function("EOMONTH", "EOMONTH(start_date, months)", "Returns the last day of a month offset.", "start_date", "months"),
        Function("FALSE", "FALSE()", "Returns the logical value FALSE."),
        Function("FIND", "FIND(find_text, within_text, [start])", "Finds text using a case-sensitive search.", "find_text", "within_text", "start"),
        Function("HLOOKUP", "HLOOKUP(value, table, row_index, [exact])", "Looks up a value across the first row of a table.", "value", "table", "row_index", "exact"),
        Function("HOUR", "HOUR(serial)", "Returns the hour component of a time.", "serial"),
        Function("IF", "IF(test, value_if_true, value_if_false)", "Returns one value when a condition is TRUE and another when FALSE.", "test", "value_if_true", "value_if_false"),
        Function("IFERROR", "IFERROR(value, fallback)", "Returns a fallback when the value is an error.", "value", "fallback"),
        Function("INDEX", "INDEX(array, row, [column])", "Returns a value from a row and column within an array.", "array", "row", "column"),
        Function("INDIRECT", "INDIRECT(reference_text)", "Resolves a text address into a reference.", "reference_text"),
        Function("ISEVEN", "ISEVEN(number)", "Returns TRUE when the number is even.", "number"),
        Function("ISBLANK", "ISBLANK(value)", "Returns TRUE when the value is blank.", "value"),
        Function("ISERROR", "ISERROR(value)", "Returns TRUE when the value is an error.", "value"),
        Function("ISLOGICAL", "ISLOGICAL(value)", "Returns TRUE when the value is TRUE or FALSE.", "value"),
        Function("ISNUMBER", "ISNUMBER(value)", "Returns TRUE when the value is numeric.", "value"),
        Function("ISODD", "ISODD(number)", "Returns TRUE when the number is odd.", "number"),
        Function("ISTEXT", "ISTEXT(value)", "Returns TRUE when the value is text.", "value"),
        Function("LEFT", "LEFT(text, [count])", "Returns the leftmost characters from text.", "text", "count"),
        Function("LEN", "LEN(text)", "Returns the text length.", "text"),
        Function("LOWER", "LOWER(text)", "Converts text to lowercase.", "text"),
        Function("MATCH", "MATCH(value, lookup_array, [match_type])", "Returns the relative position of a lookup value.", "value", "lookup_array", "match_type"),
        Function("MAX", "MAX(number1, [number2], ...)", "Returns the maximum numeric value.", "number1", "number2"),
        Function("MID", "MID(text, start, count)", "Returns characters from the middle of text.", "text", "start", "count"),
        Function("MIN", "MIN(number1, [number2], ...)", "Returns the minimum numeric value.", "number1", "number2"),
        Function("MINUTE", "MINUTE(serial)", "Returns the minute component of a time.", "serial"),
        Function("MOD", "MOD(number, divisor)", "Returns the remainder after division.", "number", "divisor"),
        Function("MONTH", "MONTH(serial)", "Returns the month number from a date.", "serial"),
        Function("NOT", "NOT(logical)", "Reverses a logical value.", "logical"),
        Function("NOW", "NOW()", "Returns the current date and time."),
        Function("OFFSET", "OFFSET(reference, rows, cols, [height], [width])", "Returns a reference offset from another reference.", "reference", "rows", "cols", "height", "width"),
        Function("OR", "OR(logical1, [logical2], ...)", "Returns TRUE when any condition is TRUE.", "logical1", "logical2"),
        Function("PI", "PI()", "Returns the value of pi."),
        Function("POWER", "POWER(number, exponent)", "Raises a number to a power.", "number", "exponent"),
        Function("PROPER", "PROPER(text)", "Capitalizes each word in text.", "text"),
        Function("RAND", "RAND()", "Returns a random number between 0 and 1."),
        Function("RANDBETWEEN", "RANDBETWEEN(bottom, top)", "Returns a random integer within a range.", "bottom", "top"),
        Function("REPT", "REPT(text, number_times)", "Repeats text a number of times.", "text", "number_times"),
        Function("RIGHT", "RIGHT(text, [count])", "Returns the rightmost characters from text.", "text", "count"),
        Function("ROUND", "ROUND(number, digits)", "Rounds a number to a number of digits.", "number", "digits"),
        Function("ROUNDDOWN", "ROUNDDOWN(number, digits)", "Rounds a number down toward zero.", "number", "digits"),
        Function("ROUNDUP", "ROUNDUP(number, digits)", "Rounds a number up away from zero.", "number", "digits"),
        Function("ROW", "ROW([reference])", "Returns the row number for a reference.", "reference"),
        Function("ROWS", "ROWS(array)", "Returns the number of rows in an array or range.", "array"),
        Function("SEARCH", "SEARCH(find_text, within_text, [start])", "Finds text using a case-insensitive search.", "find_text", "within_text", "start"),
        Function("SECOND", "SECOND(serial)", "Returns the second component of a time.", "serial"),
        Function("SQRT", "SQRT(number)", "Returns the square root.", "number"),
        Function("SUBSTITUTE", "SUBSTITUTE(text, old_text, new_text, [instance])", "Replaces existing text with new text.", "text", "old_text", "new_text", "instance"),
        Function("SUM", "SUM(number1, [number2], ...)", "Adds numeric values together.", "number1", "number2"),
        Function("TEXT", "TEXT(value, format)", "Formats a value using a number format string.", "value", "format"),
        Function("TIME", "TIME(hour, minute, second)", "Builds a serial time value.", "hour", "minute", "second"),
        Function("TIMEVALUE", "TIMEVALUE(text)", "Parses text into a time serial value.", "text"),
        Function("TODAY", "TODAY()", "Returns the current date."),
        Function("TRIM", "TRIM(text)", "Removes extra spaces from text.", "text"),
        Function("TRUE", "TRUE()", "Returns the logical value TRUE."),
        Function("UPPER", "UPPER(text)", "Converts text to uppercase.", "text"),
        Function("VALUE", "VALUE(text)", "Converts text into a numeric value.", "text"),
        Function("VLOOKUP", "VLOOKUP(value, table, column_index, [exact])", "Looks up a value down the first column of a table.", "value", "table", "column_index", "exact"),
        Function("WEEKDAY", "WEEKDAY(serial, [return_type])", "Returns the day of week number.", "serial", "return_type"),
        Function("WEEKNUM", "WEEKNUM(serial, [return_type])", "Returns the week number for a date.", "serial", "return_type"),
        Function("YEAR", "YEAR(serial)", "Returns the year from a date serial.", "serial")
    ];

    private static SpreadsheetFormulaFunctionMetadata Function(string name, string signature, string summary, params string[] arguments)
        => new()
        {
            Name = name,
            Signature = signature,
            Summary = summary,
            Arguments = arguments
        };
}
