using System.Text;
using Microsoft.AspNetCore.Html;

namespace Optical.Web.Printing;

/// <summary>
/// Draws a Code 39 barcode as inline SVG so a receipt scans at the counter.
///
/// Code 39 rather than a library: it needs no dependency and no network, which matters on a
/// till that has to keep printing when the internet is down. It covers digits, A-Z and a few
/// symbols, which is everything an invoice number is made of.
/// </summary>
public static class Code39
{
    /// <summary>
    /// Each character as twelve modules, a bar where the digit is 1 and a space where it is 0.
    /// Characters are separated by one narrow space, and the whole code is wrapped in "*".
    /// </summary>
    private static readonly Dictionary<char, string> Patterns = new()
    {
        ['0'] = "101001101101", ['1'] = "110100101011", ['2'] = "101100101011",
        ['3'] = "110110010101", ['4'] = "101001101011", ['5'] = "110100110101",
        ['6'] = "101100110101", ['7'] = "101001011011", ['8'] = "110100101101",
        ['9'] = "101100101101", ['A'] = "110101001011", ['B'] = "101101001011",
        ['C'] = "110110100101", ['D'] = "101011001011", ['E'] = "110101100101",
        ['F'] = "101101100101", ['G'] = "101010011011", ['H'] = "110101001101",
        ['I'] = "101101001101", ['J'] = "101011001101", ['K'] = "110101010011",
        ['L'] = "101101010011", ['M'] = "110110101001", ['N'] = "101011010011",
        ['O'] = "110101101001", ['P'] = "101101101001", ['Q'] = "101010110011",
        ['R'] = "110101011001", ['S'] = "101101011001", ['T'] = "101011011001",
        ['U'] = "110010101011", ['V'] = "100110101011", ['W'] = "110011010101",
        ['X'] = "100101101011", ['Y'] = "110010110101", ['Z'] = "100110110101",
        ['-'] = "100101011011", ['.'] = "110010101101", [' '] = "100110101101",
        ['$'] = "100100100101", ['/'] = "100100101001", ['+'] = "100101001001",
        ['%'] = "101001001001", ['*'] = "100101101101"
    };

    /// <summary>Blank space each side, without which a scanner cannot find the code.</summary>
    private const int QuietZoneModules = 10;

    /// <summary>
    /// The barcode for <paramref name="value"/>, or null when it holds a character Code 39
    /// cannot carry. A missing barcode is better on paper than a wrong one.
    /// </summary>
    public static HtmlString? Svg(string? value, int moduleWidth = 2, int height = 44)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().ToUpperInvariant();
        var modules = new StringBuilder();

        foreach (var character in $"*{text}*")
        {
            if (!Patterns.TryGetValue(character, out var pattern))
            {
                return null;
            }

            if (modules.Length > 0)
            {
                modules.Append('0');
            }

            modules.Append(pattern);
        }

        return Render(modules.ToString(), moduleWidth, height);
    }

    private static HtmlString Render(string modules, int moduleWidth, int height)
    {
        var width = (modules.Length + (QuietZoneModules * 2)) * moduleWidth;

        var svg = new StringBuilder()
            .Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" ")
            .Append($"width=\"{width}\" height=\"{height}\" role=\"img\" ")
            .Append("shape-rendering=\"crispEdges\" preserveAspectRatio=\"xMidYMid meet\">")
            .Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"#fff\"/>");

        // One rectangle per run of bars rather than per module, so the printer is asked to
        // draw a few dozen shapes instead of a few hundred.
        var index = 0;

        while (index < modules.Length)
        {
            if (modules[index] == '0')
            {
                index++;
                continue;
            }

            var start = index;

            while (index < modules.Length && modules[index] == '1')
            {
                index++;
            }

            var x = (QuietZoneModules + start) * moduleWidth;
            var barWidth = (index - start) * moduleWidth;

            svg.Append($"<rect x=\"{x}\" y=\"0\" width=\"{barWidth}\" height=\"{height}\" fill=\"#000\"/>");
        }

        return new HtmlString(svg.Append("</svg>").ToString());
    }
}
