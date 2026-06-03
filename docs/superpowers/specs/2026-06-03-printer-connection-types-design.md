# Printer Connection Types — Design Spec

**Date:** 2026-06-03  
**Status:** Approved

## Overview

Extend the existing printer settings UI and backend to support three connection types: **COM** (serial, existing), **USB** (Windows printer name), and **LAN** (TCP socket). The settings panel is reorganised into a tabbed interface where each tab exposes only the fields relevant to that connection type.

---

## UI Design

### Layout

The `renderPrinter(container)` function in `settings.js` is rewritten to produce:

```
[Toggle: Impressora ativada]
[Tabs: COM | USB | LAN]           ← active tab highlighted
  ↳ COM:  dropdown Porta + ↺ Atualizar | dropdown Baud Rate
  ↳ USB:  dropdown Impressora Windows + ↺ Atualizar
  ↳ LAN:  input Endereço IP | input Porta (default 9100)
[Cópias: − N +]
[Guardar]  [Teste de Impressão]
```

- Toggle "Impressora ativada" and "Cópias" are outside the tabs — they apply to all types.
- On load, read `PrinterType` from settings and activate the matching tab.
- Switching tabs shows/hides the relevant panel; does not auto-save.
- "↺ Atualizar" in COM tab calls existing `getSerialPorts`.
- "↺ Atualizar" in USB tab calls new `getWindowsPrinters`.

### Save behaviour

`savePrinterSettings` reads the active tab and sends only the fields for that type plus the common fields:

```javascript
{
  PrinterType: activeTab,       // "COM", "USB" or "LAN"
  SerialPort: ...,              // COM only
  BaudRate: ...,                // COM only
  UsbPrinterName: ...,          // USB only
  LanIpAddress: ...,            // LAN only
  LanPort: ...,                 // LAN only (default "9100")
  PrinterEnabled: ...,
  PrintCopies: ...,
}
```

After saving, the bridge rebuilds the active transport immediately (no restart required).

---

## Backend Architecture

### Interface

New file `GruderPOS/Printing/IPrinterTransport.cs`:

```csharp
public interface IPrinterTransport
{
    void Connect();
    void Disconnect();
    void Write(byte[] data);
    bool IsConnected { get; }
}
```

### Implementations

| File | Type | Mechanism |
|---|---|---|
| `SerialPortManager.cs` (existing) | COM | `System.IO.Ports.SerialPort` — implement `IPrinterTransport` |
| `WindowsPrinterTransport.cs` (new) | USB | P/Invoke to `winspool.drv` — send raw ESC/POS bytes to a named Windows printer |
| `TcpPrinterTransport.cs` (new) | LAN | `System.Net.Sockets.TcpClient` to `IP:port` |

**Connection semantics per type:**
- **COM** — persistent connection; `Connect()`/`Disconnect()` open/close the serial port as today.
- **USB** — stateless (Windows spooler manages it); `Connect()` validates the printer name exists in `InstalledPrinters`; `IsConnected` returns `true` if name is valid; `Disconnect()` is a no-op.
- **LAN** — connect-per-job; `TcpPrinterTransport.Write()` opens the TCP connection, sends bytes, and closes it. Avoids stale connections between jobs. `Connect()`/`Disconnect()` are no-ops; `IsConnected` returns `true` if the IP/port fields are non-empty.

### `ReceiptPrinter.cs`

Receives `IPrinterTransport` instead of `SerialPortManager`. All receipt layout logic is unchanged. A new `SetTransport(IPrinterTransport)` method allows hot-swapping after settings change.

### `WebBridge.cs`

**New action `getWindowsPrinters`:**
```csharp
case "getWindowsPrinters":
    var printers = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
    var current = await _settingsRepo.GetAsync("UsbPrinterName") ?? "";
    return new { printers, currentPrinter = current };
```

**Modified `saveSettings`:** after persisting to the database, invokes an `Action onSettingsSaved` callback that MainForm registers in the WebBridge constructor. This avoids WebBridge depending on MainForm directly.

### `MainForm.cs`

New method `RebuildPrinterTransport()` reads `PrinterType` and constructs the appropriate transport:

```csharp
IPrinterTransport transport = printerType switch {
    "USB" => new WindowsPrinterTransport(usbPrinterName),
    "LAN" => new TcpPrinterTransport(lanIp, int.Parse(lanPort)),
    _     => new SerialPortManager(serialPort, int.Parse(baudRate))
};
_receiptPrinter.SetTransport(transport);
```

Called at startup and wired as the `onSettingsSaved` callback passed to WebBridge — called automatically after every settings save.

---

## Settings Keys

| Key | Values | Notes |
|---|---|---|
| `PrinterType` | `"COM"`, `"USB"`, `"LAN"` | New — defaults to `"COM"` |
| `SerialPort` | `"COM3"`, etc. | Existing |
| `BaudRate` | `"9600"`, etc. | Existing |
| `UsbPrinterName` | `"EPSON TM-T20III"` | New |
| `LanIpAddress` | `"192.168.1.100"` | New |
| `LanPort` | `"9100"` | New — defaults to `"9100"` |
| `PrinterEnabled` | `"true"`, `"false"` | Existing |
| `PrintCopies` | `"1"`–`"10"` | Existing |

---

## Files Changed

| File | Change |
|---|---|
| `GruderPOS/Printing/IPrinterTransport.cs` | New — interface |
| `GruderPOS/Printing/WindowsPrinterTransport.cs` | New — USB transport |
| `GruderPOS/Printing/TcpPrinterTransport.cs` | New — LAN transport |
| `GruderPOS/Printing/SerialPortManager.cs` | Modified — implement `IPrinterTransport` |
| `GruderPOS/Printing/ReceiptPrinter.cs` | Modified — accept `IPrinterTransport`, add `SetTransport` |
| `GruderPOS/Bridge/WebBridge.cs` | Modified — add `getWindowsPrinters`, update `saveSettings` |
| `GruderPOS/MainForm.cs` | Modified — add `RebuildPrinterTransport()` |
| `GruderPOS/wwwroot/js/settings.js` | Modified — rewrite `renderPrinter`, add tab logic |

---

## Out of Scope

- Printer discovery on the network (mDNS/Bonjour)
- USB raw HID/bulk transfer
- Per-tab test-print validation (test print uses whatever transport is currently active)
