using Optical.Web.Printing;

namespace Optical.Tests;

/// <summary>
/// The barcode printed on a receipt. A scanner reads bar widths, so an encoding slip produces
/// a code that looks right on paper and reads as the wrong invoice at the counter.
/// </summary>
public class Code39Tests
{
    [Fact]
    public void An_invoice_number_encodes_to_bars()
    {
        var svg = Code39.Svg("INV-20260905-00008");

        Assert.NotNull(svg);

        var markup = svg!.Value!;

        Assert.StartsWith("<svg", markup);
        Assert.EndsWith("</svg>", markup);
        Assert.Contains("fill=\"#000\"", markup);
    }

    [Fact]
    public void The_code_is_wrapped_in_the_start_and_stop_character()
    {
        var markup = Code39.Svg("A", moduleWidth: 1, height: 10)!.Value!;

        // One character becomes three: "*", "A", "*". Three lots of twelve modules, two narrow
        // gaps between them, and a quiet zone of ten each side.
        Assert.Contains("width=\"58\"", markup);

        // "*" is 100101101101, so the very first bar sits one module wide at the quiet zone's
        // edge. Without the start character a scanner has nothing to lock on to.
        Assert.Contains("<rect x=\"10\" y=\"0\" width=\"1\" height=\"10\" fill=\"#000\"/>", markup);
    }

    [Fact]
    public void Width_grows_with_the_number_of_characters()
    {
        var shortCode = Code39.Svg("AB", moduleWidth: 1, height: 10)!.Value!;
        var longCode = Code39.Svg("ABCD", moduleWidth: 1, height: 10)!.Value!;

        // Each extra character adds its twelve modules plus the narrow gap before it.
        Assert.Contains("width=\"71\"", shortCode);
        Assert.Contains("width=\"97\"", longCode);
    }

    [Theory]
    [InlineData("inv-001")]
    [InlineData("INV 001")]
    [InlineData("12345")]
    public void Everything_an_invoice_number_is_made_of_can_be_encoded(string value) =>
        Assert.NotNull(Code39.Svg(value));

    [Theory]
    [InlineData("INV#001")]
    [InlineData("INV_001")]
    [InlineData("café")]
    public void A_character_the_symbology_cannot_carry_produces_no_barcode(string value) =>
        Assert.Null(Code39.Svg(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_to_encode_produces_no_barcode(string? value) =>
        Assert.Null(Code39.Svg(value));
}
