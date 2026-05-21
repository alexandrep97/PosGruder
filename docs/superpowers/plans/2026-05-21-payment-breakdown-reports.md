# Payment Breakdown in Session Reports — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a per-payment-type breakdown (count + total) in both the printed closing report and the session listing in the history tab, filtering out unused types, and adding Total Caixa to both views.

**Architecture:** Add two new DTOs (`PaymentBreakdown`, `CashSessionDetail`) to Models.cs; add `GetAllWithBreakdownAsync()` to `CashSessionRepository` using two SQL queries assembled in C#; update `WebBridge.HandleGetCashSessions()` to use the new method; restructure `PrintCashSessionReport()` in `ReceiptPrinter`; rewrite `renderSessions()` in `history.js` with new CSS styles.

**Tech Stack:** C# / Dapper / SQLite (backend), Vanilla JS / HTML / CSS (frontend), ESC/POS thermal printer protocol

---

## File Map

| File | Change |
|---|---|
| `GruderPOS/Data/Models.cs` | Add `PaymentBreakdown` and `CashSessionDetail` classes |
| `GruderPOS/Data/CashSessionRepository.cs` | Add `GetAllWithBreakdownAsync()` + private `PaymentBreakdownRow` helper |
| `GruderPOS/Bridge/WebBridge.cs` | Change `HandleGetCashSessions()` to call the new repository method |
| `GruderPOS/Printing/ReceiptPrinter.cs` | Restructure `PrintCashSessionReport()` totals/payment section |
| `GruderPOS/wwwroot/js/history.js` | Rewrite `renderSessions()` to consume new response shape |
| `GruderPOS/wwwroot/css/styles.css` | Add CSS for new session card layout elements |

---

## Task 1: Add DTOs to Models.cs

**Files:**
- Modify: `GruderPOS/Data/Models.cs`

- [ ] **Step 1: Add the two new classes after the `CashSession` class (after line 38)**

  Open `GruderPOS/Data/Models.cs`. After the closing `}` of `CashSession` (line 38), insert:

  ```csharp
  public class PaymentBreakdown
  {
      public string Method { get; set; } = string.Empty;
      public double Total { get; set; }
      public int Count { get; set; }
  }

  public class CashSessionDetail
  {
      public CashSession Session { get; set; } = new();
      public List<PaymentBreakdown> PaymentBreakdown { get; set; } = new();
  }
  ```

- [ ] **Step 2: Build to verify no compile errors**

  Run in terminal from `GruderPOS/`:
  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

  ```bash
  git add GruderPOS/Data/Models.cs
  git commit -m "feat: add PaymentBreakdown and CashSessionDetail DTOs"
  ```

---

## Task 2: Add GetAllWithBreakdownAsync() to CashSessionRepository

**Files:**
- Modify: `GruderPOS/Data/CashSessionRepository.cs`

- [ ] **Step 1: Add the private helper class and new public method**

  Open `GruderPOS/Data/CashSessionRepository.cs`. Add this private class and method before the closing `}` of `CashSessionRepository` (before line 72):

  ```csharp
  public async Task<IEnumerable<CashSessionDetail>> GetAllWithBreakdownAsync()
  {
      using var conn = _db.GetConnection();

      var sessions = (await conn.QueryAsync<CashSession>(
          "SELECT * FROM CashSessions ORDER BY OpenedAt DESC")).ToList();

      var rawBreakdowns = (await conn.QueryAsync<PaymentBreakdownRow>(@"
          SELECT CashSessionId, PaymentMethod,
                 SUM(TotalAmount) AS Total,
                 COUNT(*) AS Count
          FROM Transactions
          WHERE Voided = 0
          GROUP BY CashSessionId, PaymentMethod")).ToList();

      var breakdownLookup = rawBreakdowns.ToLookup(r => r.CashSessionId);

      return sessions.Select(s => new CashSessionDetail
      {
          Session = s,
          PaymentBreakdown = breakdownLookup[s.Id]
              .Select(r => new PaymentBreakdown
              {
                  Method = r.PaymentMethod,
                  Total = r.Total,
                  Count = r.Count
              })
              .ToList()
      });
  }

  private class PaymentBreakdownRow
  {
      public int CashSessionId { get; set; }
      public string PaymentMethod { get; set; } = string.Empty;
      public double Total { get; set; }
      public int Count { get; set; }
  }
  ```

- [ ] **Step 2: Build to verify no compile errors**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

  ```bash
  git add GruderPOS/Data/CashSessionRepository.cs
  git commit -m "feat: add GetAllWithBreakdownAsync with per-session payment aggregation"
  ```

---

## Task 3: Update WebBridge to use new repository method

**Files:**
- Modify: `GruderPOS/Bridge/WebBridge.cs` (line 213)

- [ ] **Step 1: Replace `HandleGetCashSessions()`**

  Find this method at line 213:
  ```csharp
  private async Task<object> HandleGetCashSessions() =>
      await _cashSessions.GetAllAsync();
  ```

  Replace with:
  ```csharp
  private async Task<object> HandleGetCashSessions() =>
      await _cashSessions.GetAllWithBreakdownAsync();
  ```

- [ ] **Step 2: Build to verify**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

  ```bash
  git add GruderPOS/Bridge/WebBridge.cs
  git commit -m "feat: wire getCashSessions to return payment breakdown"
  ```

---

## Task 4: Restructure PrintCashSessionReport() in ReceiptPrinter

**Files:**
- Modify: `GruderPOS/Printing/ReceiptPrinter.cs` (lines 413–436)

The goal is to change the payment section from:
```
Fundo Caixa / Total Vendas / Num Trans / TOTAL (big)
---
Resumo por pagamento:
  Dinheiro (3):  150.00
  Cartão (2):    200.00
```

To:
```
Fundo Caixa:   50.00
---
PAGAMENTOS:
  Dinheiro (3):  150.00
  Cartão (2):    200.00
---
TOTAL VENDAS:  350.00
TOTAL CAIXA:   400.00  (big)
```

- [ ] **Step 1: Replace the totals + payment block (lines 413–436)**

  Find and replace this entire block:
  ```csharp
          // Totals
          _serial.Write(EscPosCommands.BoldOn);
          _serial.WriteText(FormatTotalLine("Fundo Caixa:", $"{session.OpeningBalance:F2}"));
          _serial.WriteText(FormatTotalLine("Total Vendas:", $"{session.TotalSales:F2}"));
          _serial.WriteText(FormatTotalLine("Num. Trans.:", $"{session.TotalTransactions}"));

          var closing = session.ClosingBalance ?? (session.OpeningBalance + session.TotalSales);
          _serial.Write(EscPosCommands.SizeDoubleHeight);
          _serial.WriteText(FormatTotalLine("TOTAL:", $"{closing:F2} EUR"));
          _serial.Write(EscPosCommands.SizeNormal);
          _serial.Write(EscPosCommands.BoldOff);

          // Payment method breakdown
          PrintLine('-');
          _serial.WriteText("Resumo por pagamento:\n");
          var byPayment = transactions
              .Where(t => !t.Voided)
              .GroupBy(t => t.PaymentMethod)
              .Select(g => new { Method = g.Key, Total = g.Sum(t => t.TotalAmount), Count = g.Count() });

          foreach (var pm in byPayment)
          {
              _serial.WriteText(FormatTotalLine($"  {GetPaymentLabel(pm.Method)} ({pm.Count}):", $"{pm.Total:F2}"));
          }
  ```

  With:
  ```csharp
          // Opening balance
          _serial.Write(EscPosCommands.BoldOn);
          _serial.WriteText(FormatTotalLine("Fundo Caixa:", $"{session.OpeningBalance:F2}"));
          _serial.Write(EscPosCommands.BoldOff);

          // Payment method breakdown
          PrintLine('-');
          _serial.WriteText("PAGAMENTOS:\n");
          var byPayment = transactions
              .Where(t => !t.Voided)
              .GroupBy(t => t.PaymentMethod)
              .Select(g => new { Method = g.Key, Total = g.Sum(t => t.TotalAmount), Count = g.Count() });

          foreach (var pm in byPayment)
          {
              _serial.WriteText(FormatTotalLine($"  {GetPaymentLabel(pm.Method)} ({pm.Count}):", $"{pm.Total:F2}"));
          }

          // Totals summary
          PrintLine('-');
          _serial.Write(EscPosCommands.BoldOn);
          _serial.WriteText(FormatTotalLine("TOTAL VENDAS:", $"{session.TotalSales:F2}"));
          var closing = session.ClosingBalance ?? (session.OpeningBalance + session.TotalSales);
          _serial.Write(EscPosCommands.SizeDoubleHeight);
          _serial.WriteText(FormatTotalLine("TOTAL CAIXA:", $"{closing:F2} EUR"));
          _serial.Write(EscPosCommands.SizeNormal);
          _serial.Write(EscPosCommands.BoldOff);
  ```

- [ ] **Step 2: Build to verify**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

  ```bash
  git add GruderPOS/Printing/ReceiptPrinter.cs
  git commit -m "feat: restructure fecho de caixa receipt with payment breakdown and total caixa"
  ```

---

## Task 5: Add CSS for new session card layout

**Files:**
- Modify: `GruderPOS/wwwroot/css/styles.css`

- [ ] **Step 1: Add new styles after the existing `.session-stat-label` block (around line 1158)**

  Find this block and append after it:
  ```css
  .session-stat-label {
      font-size: 11px;
      color: var(--text-muted);
      margin-top: 2px;
  ```

  After the closing `}` of `.session-stat-label`, add:
  ```css

  .session-opening-balance {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 0;
      border-top: 1px solid var(--border);
      margin-top: 8px;
  }

  .session-opening-balance .session-stat-label {
      font-size: 12px;
      margin-top: 0;
  }

  .session-opening-balance .session-stat-value {
      font-size: 14px;
  }

  .session-payment-breakdown {
      border-top: 1px solid var(--border);
      padding-top: 8px;
      margin-top: 4px;
  }

  .session-breakdown-label {
      font-size: 11px;
      font-weight: 700;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 6px;
  }

  .session-breakdown-row {
      display: flex;
      justify-content: space-between;
      font-size: 13px;
      padding: 2px 0;
      color: var(--text-light);
  }

  .session-totals {
      border-top: 1px solid var(--border);
      margin-top: 8px;
      padding-top: 8px;
  }

  .session-total-row {
      display: flex;
      justify-content: space-between;
      font-size: 14px;
      font-weight: 600;
      padding: 3px 0;
  }

  .session-total-caixa {
      font-size: 16px;
      font-weight: 800;
      margin-top: 4px;
      padding-top: 4px;
      border-top: 1px solid var(--border);
  }
  ```

- [ ] **Step 2: Commit**

  ```bash
  git add GruderPOS/wwwroot/css/styles.css
  git commit -m "feat: add CSS for session card payment breakdown layout"
  ```

---

## Task 6: Rewrite renderSessions() in history.js

**Files:**
- Modify: `GruderPOS/wwwroot/js/history.js` (lines 159–213)

The bridge now returns `List<CashSessionDetail>` where each item has:
- `detail.session` — the `CashSession` object (camelCase: `id`, `openedAt`, `closedAt`, `openingBalance`, `closingBalance`, `totalSales`, `totalTransactions`, `status`, `notes`)
- `detail.paymentBreakdown` — array of `{ method, total, count }` (only used types)

- [ ] **Step 1: Replace the entire `renderSessions()` method (lines 159–213)**

  Find and replace from `// ===== Cash Sessions =====` to the final `},` of `renderSessions`:

  ```javascript
      // ===== Cash Sessions =====
      async renderSessions(container) {
          let rawSessions;
          try {
              rawSessions = await bridge.send('getCashSessions');
          } catch (e) {
              rawSessions = [];
          }

          this.sessions = rawSessions || [];

          if (this.sessions.length === 0) {
              container.innerHTML = `
                  <div class="empty-state">
                      <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
                      <p>Sem sessões de caixa registadas</p>
                  </div>`;
              return;
          }

          const paymentLabels = { Cash: 'Dinheiro', Card: 'Cartão', MBWay: 'MB Way' };

          let html = '';
          this.sessions.forEach(detail => {
              const s = detail.session;
              const breakdown = detail.paymentBreakdown || [];
              const isOpen = s.status === 'Open';
              const totalCaixa = s.closingBalance ?? ((s.openingBalance || 0) + (s.totalSales || 0));

              let breakdownHtml = '';
              if (breakdown.length > 0) {
                  breakdownHtml = `<div class="session-payment-breakdown">
                      <div class="session-breakdown-label">Pagamentos</div>`;
                  breakdown.forEach(pm => {
                      const label = paymentLabels[pm.method] || pm.method;
                      breakdownHtml += `<div class="session-breakdown-row">
                          <span>${label} (${pm.count})</span>
                          <span>${formatCurrency(pm.total)}</span>
                      </div>`;
                  });
                  breakdownHtml += `</div>`;
              }

              html += `
                  <div class="session-card">
                      <div class="session-card-header">
                          <span class="session-card-title">Sessão #${s.id}</span>
                          <span class="session-status ${isOpen ? 'open' : 'closed'}">${isOpen ? 'Aberta' : 'Fechada'}</span>
                      </div>
                      <div style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px;">
                          Abertura: ${formatDateTime(s.openedAt)}${s.closedAt ? ' · Fecho: ' + formatDateTime(s.closedAt) : ''}
                      </div>
                      <div class="session-opening-balance">
                          <span class="session-stat-label">Fundo de Caixa</span>
                          <span class="session-stat-value">${formatCurrency(s.openingBalance || 0)}</span>
                      </div>
                      ${breakdownHtml}
                      <div class="session-totals">
                          <div class="session-total-row">
                              <span>Total Vendas</span>
                              <span class="text-success">${formatCurrency(s.totalSales || 0)}</span>
                          </div>
                          ${!isOpen ? `<div class="session-total-row session-total-caixa">
                              <span>Total Caixa</span>
                              <span class="text-success">${formatCurrency(totalCaixa)}</span>
                          </div>` : ''}
                      </div>
                      ${s.notes ? `<div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border); font-size: 13px; color: var(--text-light);">Notas: ${s.notes}</div>` : ''}
                  </div>`;
          });

          container.innerHTML = html;
      }
  ```

- [ ] **Step 2: Build to verify (catches any C# issues, JS errors only appear at runtime)**

  ```
  dotnet build
  ```
  Expected: `Build succeeded.`

- [ ] **Step 3: Run the app and open the History tab → Sessões**

  Launch the app and navigate to the History tab → Sessões sub-tab.

  Verify:
  - Each session card shows Fundo de Caixa, payment breakdown rows (only used types), Total Vendas, Total Caixa
  - Sessions with no transactions show only Fundo de Caixa + Total Vendas (0.00)
  - Open sessions omit the "Total Caixa" line
  - Payment type labels are in Portuguese (Dinheiro, Cartão, MB Way)

- [ ] **Step 4: Commit**

  ```bash
  git add GruderPOS/wwwroot/js/history.js
  git commit -m "feat: show payment breakdown per type in session listing cards"
  ```

---

## Self-Review Checklist

- **Spec coverage:**
  - ✅ Payment breakdown by type in receipt — Task 4
  - ✅ Payment breakdown by type in session listing — Task 6
  - ✅ Unused payment types hidden — handled by SQL GROUP BY (only types with transactions appear) + JavaScript only renders `breakdown.length > 0` rows
  - ✅ Total Vendas in receipt — Task 4 (`TOTAL VENDAS:` line)
  - ✅ Total Caixa in receipt — Task 4 (`TOTAL CAIXA:` big line)
  - ✅ Total Vendas in session card — Task 6
  - ✅ Total Caixa in session card (closed sessions only) — Task 6
  - ✅ New DTOs — Task 1
  - ✅ SQL aggregation method — Task 2
  - ✅ WebBridge wired up — Task 3
  - ✅ CSS for new elements — Task 5

- **No placeholders:** All code blocks are complete and exact.

- **Type consistency:**
  - `PaymentBreakdown` defined in Task 1, used in Task 2 ✅
  - `CashSessionDetail` defined in Task 1, used in Tasks 2 and 3 ✅
  - `PaymentBreakdownRow` defined and used only in Task 2 ✅
  - JS: `detail.session`, `detail.paymentBreakdown`, `pm.method`, `pm.count`, `pm.total` consistent across Tasks 3 and 6 ✅
