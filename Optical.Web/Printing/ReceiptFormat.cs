namespace Optical.Web.Printing;

/// <summary>
/// The paper a receipt is being printed on. The content is the same either way; only the
/// page size and the type scale change, so a customer who asks for a proper invoice gets
/// the same figures on a sheet they can file.
/// </summary>
public enum ReceiptFormat
{
    /// <summary>80mm roll on the till printer. The everyday receipt, and the default.</summary>
    Thermal = 1,

    /// <summary>A4 sheet on an office printer.</summary>
    A4 = 2
}
