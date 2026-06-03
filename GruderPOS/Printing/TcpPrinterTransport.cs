using System.Net.Sockets;

namespace GruderPOS.Printing;

public class TcpPrinterTransport : IPrinterTransport
{
    private readonly string _ipAddress;
    private readonly int _port;

    public TcpPrinterTransport(string ipAddress, int port = 9100)
    {
        _ipAddress = ipAddress;
        _port = port;
    }

    public string IpAddress => _ipAddress;
    public int Port => _port;

    // Connect-per-job — no persistent connection.
    public bool Connect() => !string.IsNullOrWhiteSpace(_ipAddress);
    public void Disconnect() { }
    public bool IsConnected => !string.IsNullOrWhiteSpace(_ipAddress);

    public bool Write(byte[] data)
    {
        if (string.IsNullOrWhiteSpace(_ipAddress)) return false;

        try
        {
            using var client = new TcpClient();
            client.Connect(_ipAddress, _port);
            using var stream = client.GetStream();
            stream.Write(data, 0, data.Length);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TCP print error: {ex.Message}");
            return false;
        }
    }
}
