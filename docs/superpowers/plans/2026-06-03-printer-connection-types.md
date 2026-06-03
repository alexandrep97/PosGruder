# Printer Connection Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add USB (Windows printer) and LAN (TCP socket) connection types to the printer settings, replacing the single COM form with a tabbed interface where each tab shows only the relevant fields.

**Architecture:** A new `IPrinterTransport` interface abstracts all three connection types. `SerialPortManager` implements it; two new classes (`WindowsPrinterTransport`, `TcpPrinterTransport`) handle USB and LAN. `ReceiptPrinter` uses the interface. `MainForm` rebuilds the transport on settings change via a callback wired into `WebBridge`.

**Tech Stack:** C# / .NET (WinForms, System.IO.Ports, System.Net.Sockets, System.Runtime.InteropServices, System.Drawing.Printing), JavaScript (vanilla), CSS custom properties.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `GruderPOS/Printing/IPrinterTransport.cs` | Create | Common interface for all transports |
| `GruderPOS/Printing/WindowsPrinterTransport.cs` | Create | USB — raw ESC/POS via Windows spooler (P/Invoke) |
| `GruderPOS/Printing/TcpPrinterTransport.cs` | Create | LAN — raw ESC/POS via TcpClient, connect-per-job |
| `GruderPOS/Printing/SerialPortManager.cs` | Modify | Implement `IPrinterTransport` |
| `GruderPOS/Printing/ReceiptPrinter.cs` | Modify | Accept `IPrinterTransport`; add `SetTransport`; move `WriteText` logic inline |
| `GruderPOS/Bridge/WebBridge.cs` | Modify | Use `IPrinterTransport`; add `getWindowsPrinters`; update `saveSettings`; accept callback |
| `GruderPOS/MainForm.cs` | Modify | `RebuildPrinterTransport()`; wire callback to bridge |
| `GruderPOS/wwwroot/css/styles.css` | Modify | Add `.printer-type-tabs` wrapper |
| `GruderPOS/wwwroot/js/settings.js` | Modify | Rewrite `renderPrinter`; update `savePrinterSettings`; add `switchPrinterTab`, `refreshWindowsPrinters`, `_loadWindowsPrinters` |

---

### Task 1: Create `IPrinterTransport` interface

**Files:**
- Create: `GruderPOS/Printing/IPrinterTransport.cs`

- [ ] **Step 1: Create the interface file**

```csharp
namespace GruderPOS.Printing;

public interface IPrinterTransport
{
    bool Connect();
    void Disconnect();
    bool Write(byte[] data);
    bool IsConnected { get; }
}
```

- [ ] **Step 2: Make `SerialPortManager` implement it**

Open `GruderPOS/Printing/SerialPortManager.cs`. Change the class declaration from:
```csharp
public class SerialPortManager : IDisposable
```
to:
```csharp
public class SerialPortManager : IPrinterTransport, IDisposable
```

The existing `Connect()`, `Disconnect()`, `Write(byte[])`, and `IsConnected` signatures already match the interface — no other changes needed in this file.

- [ ] **Step 3: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add GruderPOS/Printing/IPrinterTransport.cs GruderPOS/Printing/SerialPortManager.cs
git commit -m "feat: add IPrinterTransport interface, SerialPortManager implements it"
```

---

### Task 2: Create `WindowsPrinterTransport` (USB)

**Files:**
- Create: `GruderPOS/Printing/WindowsPrinterTransport.cs`

- [ ] **Step 1: Create the file**

```csharp
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
            var di = new DOCINFO { pDocName = "ESC/POS", pDataType = "RAW" };
            if (StartDocPrinter(hPrinter, 1, ref di) == 0) return false;
            if (!StartPagePrinter(hPrinter))
            {
                EndDocPrinter(hPrinter);
                return false;
            }

            var ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                WritePrinter(hPrinter, ptr, data.Length, out _);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
            return true;
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
```

- [ ] **Step 2: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add GruderPOS/Printing/WindowsPrinterTransport.cs
git commit -m "feat: add WindowsPrinterTransport for USB printing via Windows spooler"
```

---

### Task 3: Create `TcpPrinterTransport` (LAN)

**Files:**
- Create: `GruderPOS/Printing/TcpPrinterTransport.cs`

- [ ] **Step 1: Create the file**

```csharp
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
```

- [ ] **Step 2: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add GruderPOS/Printing/TcpPrinterTransport.cs
git commit -m "feat: add TcpPrinterTransport for LAN printing via TCP socket"
```

---

### Task 4: Update `ReceiptPrinter` to use `IPrinterTransport`

**Files:**
- Modify: `GruderPOS/Printing/ReceiptPrinter.cs`

- [ ] **Step 1: Change the field and constructor**

Find and replace the field declaration at line ~74:
```csharp
// BEFORE
private readonly SerialPortManager _serial;
```
```csharp
// AFTER
private IPrinterTransport _transport;
```

Find and replace the constructor at line ~77:
```csharp
// BEFORE
public ReceiptPrinter(SerialPortManager serial)
{
    _serial = serial;
}
```
```csharp
// AFTER
public ReceiptPrinter(IPrinterTransport transport)
{
    _transport = transport;
}
```

- [ ] **Step 2: Add `SetTransport` method** (insert immediately after the constructor)

```csharp
public void SetTransport(IPrinterTransport transport)
{
    _transport = transport;
}
```

- [ ] **Step 3: Add private `WriteText` helper** (insert after `SetTransport`)

```csharp
private bool WriteText(string text)
{
    try
    {
        var encoding = System.Text.Encoding.GetEncoding(860);
        var data = encoding.GetBytes(text);
        return _transport.Write(data);
    }
    catch
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);
        return _transport.Write(data);
    }
}
```

- [ ] **Step 4: Replace all `_serial` references using editor Find & Replace**

In your editor, run **Find & Replace** (Ctrl+H) **within this file only**:

| Find | Replace |
|---|---|
| `_serial.WriteText(` | `WriteText(` |
| `_serial.Write(` | `_transport.Write(` |
| `_serial.Connect()` | `_transport.Connect()` |
| `_serial.Disconnect()` | `_transport.Disconnect()` |
| `_serial.IsConnected` | `_transport.IsConnected` |

Do these replacements in the order shown (WriteText before Write, to avoid partial matches).

- [ ] **Step 5: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors. If you see "SerialPortManager" still referenced in this file, you missed an occurrence — search for `_serial` and fix.

- [ ] **Step 6: Commit**

```bash
git add GruderPOS/Printing/ReceiptPrinter.cs
git commit -m "refactor: ReceiptPrinter accepts IPrinterTransport, add SetTransport"
```

---

### Task 5: Update `WebBridge`

**Files:**
- Modify: `GruderPOS/Bridge/WebBridge.cs`

- [ ] **Step 1: Update fields and constructor**

Replace the two field declarations (lines 16–17):
```csharp
// BEFORE
private readonly ReceiptPrinter _printer;
private readonly SerialPortManager _serialPort;
```
```csharp
// AFTER
private ReceiptPrinter _printer;
private IPrinterTransport _transport;
private readonly Action _onSettingsSaved;
```

Replace the constructor signature and body (lines 26–35):
```csharp
// BEFORE
public WebBridge(DatabaseManager db, SerialPortManager serialPort)
{
    _categories = new CategoryRepository(db);
    _products = new ProductRepository(db);
    _transactions = new TransactionRepository(db);
    _cashSessions = new CashSessionRepository(db);
    _cashMovements = new CashMovementRepository(db);
    _settings = new SettingsRepository(db);
    _serialPort = serialPort;
    _printer = new ReceiptPrinter(serialPort);
}
```
```csharp
// AFTER
public WebBridge(DatabaseManager db, IPrinterTransport transport, Action onSettingsSaved)
{
    _categories = new CategoryRepository(db);
    _products = new ProductRepository(db);
    _transactions = new TransactionRepository(db);
    _cashSessions = new CashSessionRepository(db);
    _cashMovements = new CashMovementRepository(db);
    _settings = new SettingsRepository(db);
    _transport = transport;
    _printer = new ReceiptPrinter(transport);
    _onSettingsSaved = onSettingsSaved;
}
```

- [ ] **Step 2: Add `SetTransport` method** (insert after the constructor)

```csharp
public void SetTransport(IPrinterTransport transport)
{
    _transport = transport;
    _printer.SetTransport(transport);
}
```

- [ ] **Step 3: Add `getWindowsPrinters` to the action switch**

In `HandleMessage`, find the switch expression around line 47. Add the new case before `_ =>`:
```csharp
"getWindowsPrinters" => await HandleGetWindowsPrinters(),
```

- [ ] **Step 4: Add `HandleGetWindowsPrinters` method**

Add this method near `HandleGetSerialPorts`:
```csharp
private async Task<object> HandleGetWindowsPrinters()
{
    var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters
        .Cast<string>()
        .ToList();
    var currentPrinter = await _settings.GetAsync("UsbPrinterName") ?? "";
    return new { printers, currentPrinter };
}
```

- [ ] **Step 5: Update `HandleGetSerialPorts` to read from DB**

Replace (it becomes async):
```csharp
// BEFORE
private object HandleGetSerialPorts()
{
    var ports = SerialPortManager.GetAvailablePorts();
    return new { ports, currentPort = _serialPort.PortName, baudRate = _serialPort.BaudRate };
}
```
```csharp
// AFTER
private async Task<object> HandleGetSerialPorts()
{
    var ports = SerialPortManager.GetAvailablePorts();
    var currentPort = await _settings.GetAsync("SerialPort") ?? "COM3";
    var baudRate = int.Parse(await _settings.GetAsync("BaudRate") ?? "9600");
    return new { ports, currentPort, baudRate };
}
```

Update the switch case to await it (it was previously not awaited):
```csharp
// BEFORE
"getSerialPorts" => HandleGetSerialPorts(),
```
```csharp
// AFTER
"getSerialPorts" => await HandleGetSerialPorts(),
```

- [ ] **Step 6: Update `HandleSaveSettings`**

Replace the method body:
```csharp
// BEFORE
private async Task<object> HandleSaveSettings(JsonElement root)
{
    var data = root.GetProperty("data");
    var settings = new Dictionary<string, string>();

    foreach (var prop in data.EnumerateObject())
    {
        settings[prop.Name] = prop.Value.ToString();
    }

    await _settings.SetMultipleAsync(settings);

    // Apply serial port settings if changed
    if (settings.TryGetValue("SerialPort", out var port))
    {
        var baudRate = settings.TryGetValue("BaudRate", out var br) ? int.Parse(br) : 9600;
        _serialPort.Configure(port, baudRate);
    }

    return new { saved = true };
}
```
```csharp
// AFTER
private async Task<object> HandleSaveSettings(JsonElement root)
{
    var data = root.GetProperty("data");
    var settings = new Dictionary<string, string>();

    foreach (var prop in data.EnumerateObject())
    {
        settings[prop.Name] = prop.Value.ToString();
    }

    await _settings.SetMultipleAsync(settings);
    _onSettingsSaved.Invoke(); // MainForm rebuilds and swaps the transport

    return new { saved = true };
}
```

- [ ] **Step 7: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: 0 errors. If you see errors about `_serialPort` still referenced, search for it in WebBridge.cs and remove or replace.

- [ ] **Step 8: Commit**

```bash
git add GruderPOS/Bridge/WebBridge.cs
git commit -m "feat: WebBridge uses IPrinterTransport, add getWindowsPrinters action, settings callback"
```

---

### Task 6: Update `MainForm`

**Files:**
- Modify: `GruderPOS/MainForm.cs`

- [ ] **Step 1: Change the `_serialPort` field to `_transport`**

Replace the field declaration (line 14):
```csharp
// BEFORE
private SerialPortManager _serialPort = null!;
```
```csharp
// AFTER
private IPrinterTransport _transport = null!;
```

- [ ] **Step 2: Replace `InitializeDatabase`**

```csharp
// BEFORE
private void InitializeDatabase()
{
    _dbManager = new DatabaseManager();
    _dbManager.Initialize();

    _serialPort = new SerialPortManager();

    // Load serial port settings
    var settingsRepo = new Data.SettingsRepository(_dbManager);
    var port = settingsRepo.GetAsync("SerialPort").GetAwaiter().GetResult() ?? "COM3";
    var baudRate = int.Parse(settingsRepo.GetAsync("BaudRate").GetAwaiter().GetResult() ?? "9600");
    _serialPort.Configure(port, baudRate);

    _bridge = new WebBridge(_dbManager, _serialPort);
}
```
```csharp
// AFTER
private void InitializeDatabase()
{
    _dbManager = new DatabaseManager();
    _dbManager.Initialize();

    _transport = BuildTransport();

    _bridge = new WebBridge(_dbManager, _transport, RebuildPrinterTransport);
}
```

- [ ] **Step 3: Add `BuildTransport` and `RebuildPrinterTransport` methods**

Add these two methods to `MainForm`, after `InitializeDatabase`:

```csharp
private IPrinterTransport BuildTransport()
{
    var repo = new Data.SettingsRepository(_dbManager);
    var type = repo.GetAsync("PrinterType").GetAwaiter().GetResult() ?? "COM";

    return type switch
    {
        "USB" => new WindowsPrinterTransport(
            repo.GetAsync("UsbPrinterName").GetAwaiter().GetResult() ?? ""),
        "LAN" => new TcpPrinterTransport(
            repo.GetAsync("LanIpAddress").GetAwaiter().GetResult() ?? "127.0.0.1",
            int.Parse(repo.GetAsync("LanPort").GetAwaiter().GetResult() ?? "9100")),
        _ => BuildSerialTransport(repo)
    };
}

private SerialPortManager BuildSerialTransport(Data.SettingsRepository repo)
{
    var port = repo.GetAsync("SerialPort").GetAwaiter().GetResult() ?? "COM3";
    var baudRate = int.Parse(repo.GetAsync("BaudRate").GetAwaiter().GetResult() ?? "9600");
    var mgr = new SerialPortManager();
    mgr.Configure(port, baudRate);
    return mgr;
}

private void RebuildPrinterTransport()
{
    _transport = BuildTransport();
    _bridge.SetTransport(_transport);
}
```

- [ ] **Step 4: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add GruderPOS/MainForm.cs
git commit -m "feat: MainForm builds transport from settings, wires RebuildPrinterTransport callback"
```

---

### Task 7: Add CSS for printer type tabs

**Files:**
- Modify: `GruderPOS/wwwroot/css/styles.css`

- [ ] **Step 1: Add `.printer-type-tabs` wrapper style**

Open `styles.css` and find the `/* ===== Settings Content =====` comment (around line 993). Insert the following block just before it:

```css
/* ===== Printer Type Tabs ===== */
.printer-type-tabs {
    display: flex;
    gap: 4px;
    background: var(--bg-dark);
    padding: 4px;
    border-radius: var(--radius-sm);
    margin-bottom: 16px;
}
```

The `.tab-btn` and `.tab-btn.active` classes already exist in the stylesheet and will be reused.

- [ ] **Step 2: Build and verify**

No build step for CSS — just confirm the file is saved.

- [ ] **Step 3: Commit**

```bash
git add GruderPOS/wwwroot/css/styles.css
git commit -m "style: add printer-type-tabs wrapper for printer settings"
```

---

### Task 8: Rewrite printer settings in `settings.js`

**Files:**
- Modify: `GruderPOS/wwwroot/js/settings.js`

- [ ] **Step 1: Replace `renderPrinter`**

Find `renderPrinter(container)` (line 671) and replace the entire function with:

```javascript
async renderPrinter(container) {
    let portsData = { ports: [], currentPort: 'COM3', baudRate: 9600 };
    try { portsData = await bridge.send('getSerialPorts'); } catch (e) {}

    const appSettings = await bridge.send('getSettings').catch(() => ({}));
    const printerType = appSettings.PrinterType || 'COM';
    const printerEnabled = appSettings.PrinterEnabled !== 'false';
    const printCopies = parseInt(appSettings.PrintCopies) || 1;

    const comPortOptions = (portsData.ports?.length
        ? portsData.ports.map(p =>
            `<option value="${p}" ${p === (appSettings.SerialPort || portsData.currentPort) ? 'selected' : ''}>${p}</option>`)
        : [`<option value="COM3">COM3 (default)</option>`]
    ).join('');

    const baudOptions = [9600, 19200, 38400, 57600, 115200].map(b =>
        `<option value="${b}" ${b === (parseInt(appSettings.BaudRate) || 9600) ? 'selected' : ''}>${b}</option>`
    ).join('');

    container.innerHTML = `
        <div class="settings-form">
            <h3 style="font-size:16px;margin-bottom:20px;">Configuração da Impressora</h3>

            <div class="form-group">
                <label class="form-checkbox">
                    <input type="checkbox" id="printer-enabled" ${printerEnabled ? 'checked' : ''}>
                    <span>Impressora ativada</span>
                </label>
            </div>

            <div class="printer-type-tabs">
                <button class="tab-btn ${printerType === 'COM' ? 'active' : ''}" onclick="settings.switchPrinterTab('COM')">COM</button>
                <button class="tab-btn ${printerType === 'USB' ? 'active' : ''}" onclick="settings.switchPrinterTab('USB')">USB</button>
                <button class="tab-btn ${printerType === 'LAN' ? 'active' : ''}" onclick="settings.switchPrinterTab('LAN')">LAN</button>
            </div>

            <div id="tab-panel-COM" ${printerType !== 'COM' ? 'style="display:none"' : ''}>
                <div class="form-group">
                    <label>Porta COM</label>
                    <div style="display:flex;gap:8px;align-items:center;">
                        <select id="printer-port" class="form-select" style="flex:1;">${comPortOptions}</select>
                        <button class="btn btn-secondary" onclick="settings.refreshPorts(this)">↺</button>
                    </div>
                </div>
                <div class="form-group">
                    <label>Baud Rate</label>
                    <select id="printer-baud" class="form-select">${baudOptions}</select>
                </div>
            </div>

            <div id="tab-panel-USB" ${printerType !== 'USB' ? 'style="display:none"' : ''}>
                <div class="form-group">
                    <label>Impressora Windows</label>
                    <div style="display:flex;gap:8px;align-items:center;">
                        <select id="printer-usb-name" class="form-select" style="flex:1;">
                            <option value="">A carregar...</option>
                        </select>
                        <button class="btn btn-secondary" onclick="settings.refreshWindowsPrinters(this)">↺</button>
                    </div>
                </div>
            </div>

            <div id="tab-panel-LAN" ${printerType !== 'LAN' ? 'style="display:none"' : ''}>
                <div class="form-group">
                    <label>Endereço IP</label>
                    <input type="text" id="printer-lan-ip" class="form-input"
                        value="${appSettings.LanIpAddress || ''}" placeholder="192.168.1.100">
                </div>
                <div class="form-group">
                    <label>Porta</label>
                    <input type="number" id="printer-lan-port" class="form-input"
                        value="${appSettings.LanPort || '9100'}" min="1" max="65535" style="width:120px;">
                </div>
            </div>
        </div>

        <div class="settings-form" style="margin-top:16px;">
            <h3 style="font-size:16px;margin-bottom:8px;">Vias de Impressão</h3>
            <p style="font-size:12px;color:var(--text-muted);margin-bottom:16px;">
                Número de cópias idênticas impressas por cada pagamento.
            </p>
            <div class="form-group">
                <label>Quantidade de vias</label>
                <div style="display:flex;align-items:center;gap:12px;">
                    <button class="qty-btn" onclick="settings.adjustCopies(-1)" style="width:44px;height:44px;font-size:20px;">−</button>
                    <span id="printer-copies-value" style="font-size:28px;font-weight:800;min-width:40px;text-align:center;">${printCopies}</span>
                    <button class="qty-btn" onclick="settings.adjustCopies(1)" style="width:44px;height:44px;font-size:20px;">+</button>
                </div>
            </div>
        </div>

        <div style="display:flex;gap:8px;margin-top:16px;">
            <button class="btn btn-primary" onclick="settings.savePrinterSettings(this)">Guardar</button>
            <button class="btn btn-outline" onclick="settings.testPrint(this)">Teste de Impressão</button>
        </div>`;

    if (printerType === 'USB') {
        await this._loadWindowsPrinters(appSettings.UsbPrinterName || '');
    }
},
```

- [ ] **Step 2: Replace `savePrinterSettings`**

Find `async savePrinterSettings(btn)` (line ~746) and replace it:

```javascript
async savePrinterSettings(btn) {
    const enabled = document.getElementById('printer-enabled').checked;
    const copies = document.getElementById('printer-copies-value').textContent;
    const activeTab = document.querySelector('.printer-type-tabs .tab-btn.active')?.textContent || 'COM';

    const data = {
        PrinterType: activeTab,
        PrinterEnabled: String(enabled),
        PrintCopies: copies,
    };

    if (activeTab === 'COM') {
        data.SerialPort = document.getElementById('printer-port').value;
        data.BaudRate = document.getElementById('printer-baud').value;
    } else if (activeTab === 'USB') {
        data.UsbPrinterName = document.getElementById('printer-usb-name').value;
    } else if (activeTab === 'LAN') {
        data.LanIpAddress = document.getElementById('printer-lan-ip').value;
        data.LanPort = document.getElementById('printer-lan-port').value;
    }

    setButtonLoading(btn, true);
    try {
        await bridge.send('saveSettings', { data });
        showToast('Configuração da impressora guardada!', 'success');
    } catch (e) {
        showToast('Erro: ' + e.message, 'error');
    } finally {
        setButtonLoading(btn, false);
    }
},
```

- [ ] **Step 3: Replace `refreshPorts`**

Find `async refreshPorts(btn)` (line ~781) and replace it:

```javascript
async refreshPorts(btn) {
    setButtonLoading(btn, true);
    try {
        const data = await bridge.send('getSerialPorts');
        const select = document.getElementById('printer-port');
        if (!select) return;
        const current = select.value;
        select.innerHTML = (data.ports?.length
            ? data.ports.map(p => `<option value="${p}" ${p === (current || data.currentPort) ? 'selected' : ''}>${p}</option>`)
            : [`<option value="COM3">COM3 (default)</option>`]
        ).join('');
        showToast('Portas atualizadas', 'info');
    } catch (e) {
        showToast('Erro ao atualizar portas: ' + e.message, 'error');
    } finally {
        setButtonLoading(btn, false);
    }
},
```

- [ ] **Step 4: Add new helper methods** (insert after `refreshPorts`)

```javascript
switchPrinterTab(type) {
    document.querySelectorAll('.printer-type-tabs .tab-btn')
        .forEach(b => b.classList.toggle('active', b.textContent === type));
    ['COM', 'USB', 'LAN'].forEach(t => {
        const panel = document.getElementById(`tab-panel-${t}`);
        if (panel) panel.style.display = t === type ? '' : 'none';
    });
    if (type === 'USB') {
        const current = document.getElementById('printer-usb-name')?.value || '';
        this._loadWindowsPrinters(current);
    }
},

async _loadWindowsPrinters(currentPrinter) {
    const select = document.getElementById('printer-usb-name');
    if (!select) return;
    try {
        const data = await bridge.send('getWindowsPrinters');
        select.innerHTML = (data.printers?.length
            ? data.printers.map(p =>
                `<option value="${p}" ${p === currentPrinter ? 'selected' : ''}>${p}</option>`)
            : [`<option value="">Nenhuma impressora encontrada</option>`]
        ).join('');
    } catch (e) {
        select.innerHTML = '<option value="">Erro ao carregar impressoras</option>';
    }
},

async refreshWindowsPrinters(btn) {
    setButtonLoading(btn, true);
    try {
        const data = await bridge.send('getWindowsPrinters');
        const select = document.getElementById('printer-usb-name');
        if (!select) return;
        const current = select.value;
        select.innerHTML = (data.printers?.length
            ? data.printers.map(p =>
                `<option value="${p}" ${p === current ? 'selected' : ''}>${p}</option>`)
            : [`<option value="">Nenhuma impressora encontrada</option>`]
        ).join('');
        showToast('Impressoras atualizadas', 'info');
    } catch (e) {
        showToast('Erro ao obter impressoras: ' + e.message, 'error');
    } finally {
        setButtonLoading(btn, false);
    }
},
```

- [ ] **Step 5: Build and verify**

```
dotnet build GruderPOS/GruderPOS.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add GruderPOS/wwwroot/js/settings.js
git commit -m "feat: rewrite printer settings UI with COM/USB/LAN tabs"
```

---

### Task 9: Manual end-to-end verification

- [ ] **Step 1: Run the app**

```
dotnet run --project GruderPOS/GruderPOS.csproj
```

- [ ] **Step 2: Verify COM tab (existing behaviour)**

1. Open Definições → Impressora
2. Confirm the COM tab is active by default
3. Verify the COM port dropdown populates and the ↺ button refreshes it
4. Change a setting and click Guardar — confirm toast "Configuração da impressora guardada!"

- [ ] **Step 3: Verify USB tab**

1. Click the USB tab — the COM panel disappears, the USB panel appears
2. The "Impressora Windows" dropdown shows installed printers (or empty if none)
3. Click ↺ — dropdown refreshes
4. Select a printer, click Guardar
5. Navigate away and back to Impressora — USB tab is active and the printer is pre-selected

- [ ] **Step 4: Verify LAN tab**

1. Click the LAN tab — the LAN panel appears
2. Enter an IP (e.g. `192.168.1.100`) and confirm port defaults to `9100`
3. Click Guardar
4. Navigate away and back — LAN tab is active, IP and port are restored

- [ ] **Step 5: Verify tab state persists across restarts**

1. Save with LAN selected
2. Close and reopen the app
3. Open Definições → Impressora — confirm LAN tab is active

- [ ] **Step 6: Commit**

No code changes for this task. If bugs are found, fix them before marking complete.
