using System.Runtime.InteropServices;

namespace GruderPOS.Printing;

public class WindowsPrinterTransport : IPrinterTransport
{
    private readonly string _printerName;

    public WindowsPrinterTransport(string printerName)
    {
        _printerName = printerName;
    }

    public string PrinterName => _printerName;

    // Stateless — Windows spooler manages the connection.
    // Connect() validates the printer exists; IsConnected reflects that.
    public bool Connect()
    {
        return System.Drawing.Printing.PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => p.Equals(_printerName, StringComparison.OrdinalIgnoreCase));
    }

    public void Disconnect() { }

    public bool IsConnected =>
        System.Drawing.Printing.PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => p.Equals(_printerName, StringComparison.OrdinalIgnoreCase));

    public bool Write(byte[] data)
    {
        if (string.IsNullOrWhiteSpace(_printerName)) return false;

        if (!OpenPrinter(_printerName, out var hPrinter, IntPtr.Zero))
            return false;

        try
        {
            var di = new DOCINFO { cbSize = Marshal.SizeOf<DOCINFO>(), pDocName = "ESC/POS", pDataType = "RAW" };
            if (StartDocPrinter(hPrinter, 1, ref di) == 0) return false;
            try
            {
                if (!StartPagePrinter(hPrinter)) return false;

                var ptr = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, ptr, data.Length);
                    if (!WritePrinter(hPrinter, ptr, data.Length, out var written) || written != data.Length)
                        return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                EndPagePrinter(hPrinter);
                return true;
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"USB print error: {ex.Message}");
            return false;
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFO
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pDataType;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFO di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBuf, int cbBuf, out int pcWritten);
}
