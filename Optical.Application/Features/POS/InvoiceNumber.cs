namespace Optical.Application.Features.POS;

/// <summary>
/// Builds the invoice number a customer sees on their receipt, e.g. INV-20260905-00007:
/// a fixed prefix, the day of the sale, and that day's running count.
/// </summary>
public static class InvoiceNumber
{
    /// <summary>
    /// The same on every receipt. It deliberately does not follow the business name: a shop
    /// that renames itself would otherwise start a second numbering series mid-year.
    /// </summary>
    public const string Prefix = "INV";

    private const int SequenceDigits = 5;

    public static string DayPrefix(string prefix, DateOnly date) => $"{prefix}-{date:yyyyMMdd}-";

    public static string Build(string prefix, DateOnly date, int sequence) =>
        DayPrefix(prefix, date) + sequence.ToString(new string('0', SequenceDigits));

    /// <summary>
    /// The short form printed as a barcode: just the digits, and without the century.
    /// "INV-20260905-00009" becomes "26090500009".
    ///
    /// Every character costs bars, and a receipt is only 80mm wide, so the constant prefix and
    /// the dashes are dropped — they identify nothing. What is left still finds the sale,
    /// because searching ignores dashes too.
    /// </summary>
    public static string ScanCode(string invoiceNumber)
    {
        var digits = new string(invoiceNumber.Where(char.IsDigit).ToArray());

        // A four-digit year leaves a leading "20" that will be the same for a lifetime.
        return digits.Length > 2 && digits.StartsWith("20")
            ? digits[2..]
            : digits;
    }

    /// <summary>Reads the running count back off a number this class produced.</summary>
    public static int SequenceOf(string invoiceNumber)
    {
        var lastDash = invoiceNumber.LastIndexOf('-');

        return lastDash >= 0 && int.TryParse(invoiceNumber[(lastDash + 1)..], out var sequence)
            ? sequence
            : 0;
    }
}
