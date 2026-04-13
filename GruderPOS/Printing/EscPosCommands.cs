namespace GruderPOS.Printing;

/// <summary>
/// Constantes ESC/POS para a impressora appPOS80AM3
/// </summary>
public static class EscPosCommands
{
    // Initialize
    public static readonly byte[] Initialize = { 0x1B, 0x40 };

    // Text alignment
    public static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
    public static readonly byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
    public static readonly byte[] AlignRight = { 0x1B, 0x61, 0x02 };

    // Text emphasis
    public static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
    public static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };

    // Underline
    public static readonly byte[] UnderlineOn = { 0x1B, 0x2D, 0x01 };
    public static readonly byte[] UnderlineOff = { 0x1B, 0x2D, 0x00 };

    // Character size (GS ! n)
    public static readonly byte[] SizeNormal = { 0x1D, 0x21, 0x00 };
    public static readonly byte[] SizeDoubleHeight = { 0x1D, 0x21, 0x01 };
    public static readonly byte[] SizeDoubleWidth = { 0x1D, 0x21, 0x10 };
    public static readonly byte[] SizeDouble = { 0x1D, 0x21, 0x11 };

    // Line spacing
    public static readonly byte[] DefaultLineSpacing = { 0x1B, 0x32 };

    // Feed
    public static readonly byte[] LineFeed = { 0x0A };
    public static byte[] FeedLines(int n) => new byte[] { 0x1B, 0x64, (byte)n };

    // Paper cut
    public static readonly byte[] PartialCut = { 0x1D, 0x56, 0x01 };
    public static readonly byte[] FullCut = { 0x1D, 0x56, 0x00 };

    // Cash drawer
    public static readonly byte[] OpenCashDrawer = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

    // Character code table - Western European (CP1252/CP860 for Portuguese)
    public static readonly byte[] SetCodePage860 = { 0x1B, 0x74, 0x03 }; // CP860 Portuguese
    public static readonly byte[] SetCodePage1252 = { 0x1B, 0x74, 0x10 }; // Windows-1252
}
