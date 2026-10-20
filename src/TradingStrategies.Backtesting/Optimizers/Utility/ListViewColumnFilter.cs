using System;
using System.Collections.Generic;
using System.Globalization;

// Example input: v1;v2:v3;v4. Where v2:v3 is a range of acceptable numeric values and v1,v4 are individual values

public class ListViewColumnFilter
{
    public const char IndividualSeparator = ';';
    public const char RangeSeparator = ':';

    private readonly HashSet<string> _exactValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(double Min, double Max)> _ranges = new();

    public string RawInput { get; }

    private bool DoNotFilter { get; set; } = false;
    public static ListViewColumnFilter NoFilter { get; } = new ListViewColumnFilter(string.Empty)
    {
        DoNotFilter = true
    };

    public ListViewColumnFilter(string filterInput)
    {
        RawInput = filterInput ?? string.Empty;
        Parse();
    }

    public bool IsMatch(string value)
    {
        if (DoNotFilter) return true;

        if (string.IsNullOrWhiteSpace(value))
        {
            // Если ячейка пустая, проверяем, есть ли пустая строка в точных совпадениях
            return _exactValues.Contains(string.Empty);
        }

        string cleanValue = value.Trim();

        // Проверка на точное совпадение строк (без учета регистра)
        if (_exactValues.Contains(cleanValue)) return true;

        // Проверка на попадание в числовые диапазоны
        if (TryParseNumeric(cleanValue, out var numericValue))
        {
            foreach (var range in _ranges)
            {
                if (numericValue >= range.Min && numericValue <= range.Max)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Parse()
    {
        if (string.IsNullOrWhiteSpace(RawInput)) return;

        var parts = RawInput.Split([IndividualSeparator], StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var cleanPart = part.Trim();

            // Проверяем, является ли это диапазоном
            if (cleanPart.Contains(RangeSeparator))
            {
                var rangeParts = cleanPart.Split(RangeSeparator);
                if (rangeParts.Length == 2 &&
                    TryParseNumeric(rangeParts[0], out double min) &&
                    TryParseNumeric(rangeParts[1], out double max))
                {
                    // Гарантируем, что Min всегда меньше или равен Max
                    _ranges.Add(min <= max ? (min, max) : (max, min));
                    continue;
                }
            }

            // Если не диапазон, добавляем как точное строковое значение
            _exactValues.Add(cleanPart);
        }
    }

    private static bool TryParseNumeric(string input, out double result)
    {
        return double.TryParse(input.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }
}
