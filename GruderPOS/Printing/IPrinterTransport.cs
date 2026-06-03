namespace GruderPOS.Printing;

public interface IPrinterTransport
{
    bool Connect();
    void Disconnect();
    bool Write(byte[] data);
    bool IsConnected { get; }
}
