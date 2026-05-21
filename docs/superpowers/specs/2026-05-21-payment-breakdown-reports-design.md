# Payment Breakdown in Session Reports

**Date:** 2026-05-21  
**Scope:** Cash register closing report (printed) + session listing (history UI)

---

## Goal

Show a detailed breakdown of total value and transaction count per payment type in:
1. The printed closing report ("Fecho de Caixa")
2. The session listing in the history tab

Payment types with zero transactions in a session must be hidden in both views.

---

## Payment Types

The system supports three payment methods stored as strings in the `Transactions` table:

| Stored Value | Display Label |
|---|---|
| `Cash` | Dinheiro |
| `Card` | Cartão |
| `MBWay` | MB Way |

---

## Backend Changes

### New DTOs in `GruderPOS/Data/Models.cs`

```csharp
public class PaymentBreakdown
{
    public string Method { get; set; }   // "Cash", "Card", "MBWay"
    public double Total { get; set; }
    public int Count { get; set; }
}

public class CashSessionDetail
{
    public CashSession Session { get; set; }
    public List<PaymentBreakdown> PaymentBreakdown { get; set; }
}
```

### New method in `GruderPOS/Data/CashSessionRepository.cs`

`GetAllWithBreakdownAsync()` — fetches all sessions with their payment breakdown in a single query:

```sql
SELECT
    cs.*,
    t.PaymentMethod,
    SUM(t.TotalAmount) AS PaymentTotal,
    COUNT(t.Id) AS PaymentCount
FROM CashSessions cs
LEFT JOIN Transactions t ON t.CashSessionId = cs.Id AND t.Voided = 0
GROUP BY cs.Id, t.PaymentMethod
ORDER BY cs.Id DESC
```

The method assembles `CashSessionDetail` objects by grouping the flat rows by session, filtering out NULL payment rows (sessions with no transactions), and building the `PaymentBreakdown` list per session.

### `WebBridge.cs` — `getCashSessions` handler

Changes from returning `List<CashSession>` to returning `List<CashSessionDetail>` using the new repository method.

---

## Printed Report Changes (`GruderPOS/Printing/ReceiptPrinter.cs`)

Method: `PrintCashSessionReport()`

### New section layout (payments area)

```
PAGAMENTOS:
  Dinheiro (3):       150.00
  Cartão (2):         200.00
--------------------------------
TOTAL VENDAS:         350.00
TOTAL CAIXA:          400.00
```

**Rules:**
- Only payment types with `Count > 0` are printed (already ensured by GroupBy, now made explicit)
- A separator line appears between the payment breakdown and the summary totals
- `TOTAL VENDAS` = `session.TotalSales` (unchanged)
- `TOTAL CAIXA` = `session.OpeningBalance + session.TotalSales` (= `session.ClosingBalance`)
- No "Diferença/Lucro" line

---

## Session Listing Changes (`GruderPOS/wwwroot/js/history.js`)

Function: `renderSessions()`

### Card layout per session

```
Sessão #5                              [Fechada]
01/05/2026 09:00 → 01/05/2026 18:30

Fundo de Caixa:                        50,00 €
─────────────────────────────────────────────
PAGAMENTOS
  Dinheiro         (3)               150,00 €
  Cartão           (2)               200,00 €
─────────────────────────────────────────────
Total Vendas:                        350,00 €
Total Caixa:                         400,00 €

[Ver Transações]
```

**Rules:**
- Payment rows only appear for types where `Count > 0`
- Payment labels mapped: `{ Cash: 'Dinheiro', Card: 'Cartão', MBWay: 'MB Way' }`
- Sessions with status `Open` omit the `ClosedAt` and `Total Caixa` lines
- `Total Caixa` = `session.OpeningBalance + session.TotalSales`
- Data comes from the `paymentBreakdown` array in the `CashSessionDetail` response

---

## Data Flow

```
getCashSessions (WebBridge)
  └─ CashSessionRepository.GetAllWithBreakdownAsync()
       └─ SQL: sessions JOIN transactions GROUP BY payment type
            └─ List<CashSessionDetail>
                 ├─ history.js renderSessions() → card with breakdown
                 └─ (closing report uses separate path via PrintCashSessionReport)
```

The printed receipt still uses `getSessionTransactions` data passed directly to `PrintCashSessionReport` — no change to that flow, only the internal formatting changes.

---

## Out of Scope

- Adding new payment types
- Breakdown in transaction-level receipts (per-sale receipts)
- Profit/difference ("Diferença/Lucro") line — removed by user decision
- Any changes to how transactions are recorded
