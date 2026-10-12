using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GTIP
{
    internal static class EditorNaming
    {
        internal static string Number(int value, int style)
        {
            if (value < 1 || value > 99999) throw new InvalidOperationException("Number must be between 1 and 99,999.");
            if (style == 1) return value.ToString(CultureInfo.InvariantCulture);
            if (style == 3) return value + ".";
            if (style == 2)
            {
                if (value > 3999) throw new InvalidOperationException("Roman numbering supports up to 3,999.");
                string result = ""; int[] amounts = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
                string[] labels = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
                for (int i = 0; i < amounts.Length; i++) while (value >= amounts[i]) { result += labels[i]; value -= amounts[i]; }
                return result;
            }
            int tail = value % 100;
            return value + (tail >= 11 && tail <= 13 ? "th" : value % 10 == 1 ? "st" : value % 10 == 2 ? "nd" : value % 10 == 3 ? "rd" : "th");
        }
        internal static string Render(string template, int number, int numberStyle, int casing, string state, string type, string tier, string old)
        {
            string rendered = Regex.Replace(template, @"\{([^{}]+)\}", match => {
                switch (match.Groups[1].Value)
                {
                    case "number": return Number(number, numberStyle);
                    case "state": return state;
                    case "type": return type;
                    case "tier": return tier;
                    case "old": return old;
                    default: throw new InvalidOperationException("Unknown naming token: " + match.Value);
                }
            });
            if (rendered.Contains("{") || rendered.Contains("}")) throw new InvalidOperationException("Unmatched template braces.");
            rendered = casing == 1 ? rendered.ToUpperInvariant() : casing == 2 ? rendered.ToLowerInvariant() : casing == 3 ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rendered.ToLowerInvariant()) : rendered;
            return (string)LiveEditSession.Name(rendered);
        }
    }
}
