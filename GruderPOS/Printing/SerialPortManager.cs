using System.IO.Ports;

namespace GruderPOS.Printing;

public class SerialPortManager : IDisposable
{
    private SerialPort? _port;
    private string _portName = "COM3";
    private int _baudRate = 9600;

    public bool IsConnected => _port?.IsOpen ?? false;
    public string PortName => _portName;
    public int BaudRate => _baudRate;

    public void Configure(string portName, int baudRate = 9600)
    {
        _portName = portName;
        _baudRate = baudRate;
        Disconnect();
    }

    public bool Connect()
    {
        try
        {
            if (_port?.IsOpen == true) return true;

            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                DtrEnable = true,
                RtsEnable = true
            };

            _port.Open();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Serial port error: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_port?.IsOpen == true)
            {
                _port.Close();
            }
            _port?.Dispose();
            _port = null;
        }
        catch { }
    }

    public bool Write(byte[] data)
    {
        try
        {
            if (_port?.IsOpen != true)
            {
                if (!Connect()) return false;
            }

            _port!.Write(data, 0, data.Length);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Write error: {ex.Message}");
            return false;
        }
    }

    public bool WriteText(string text)
    {
        try
        {
            var encoding = System.Text.Encoding.GetEncoding(860); // CP860 Portuguese
            var data = encoding.GetBytes(text);
            return Write(data);
        }
        catch
        {
            var data = System.Text.Encoding.UTF8.GetBytes(text);
            return Write(data);
        }
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public void Dispose()
    {
        Disconnect();
    }
}
