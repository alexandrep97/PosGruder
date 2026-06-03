# Cash Movements — Depósitos e Levantamentos de Caixa

**Data:** 2026-06-03  
**Projeto:** GruderPOS  
**Estado:** Aprovado

---

## Objetivo

Permitir que o operador registe depósitos e levantamentos de dinheiro em caixa durante uma sessão aberta, com reflexo nos saldos, no modal de fecho, no talão de fecho, e com impressão de talão individual por movimento. Cada movimento suporta um campo de notas livres (ex: nome do operador, motivo).

---

## Decisões de Design

| Questão | Decisão |
|---------|---------|
| Como aceder | Dropdown no indicador "Caixa Aberta" na sidebar |
| Campos do formulário | Valor (€) + Notas livres (textarea) |
| Talão de fecho | Depósitos/levantamentos integrados no cálculo do saldo |
| Talão individual | Minimalista: cabeçalho, data/hora, tipo, valor, notas |
| Modal de fecho | 2 novos cards coloridos: Depósitos (azul) e Levantamentos (laranja) |
| Armazenamento | Nova tabela `CashMovements` — não contamina `Transactions` |

---

## Camada de Dados

### Nova tabela `CashMovements`

```sql
CREATE TABLE IF NOT EXISTS CashMovements (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CashSessionId INTEGER NOT NULL,
    Type TEXT NOT NULL,         -- 'Deposit' | 'Withdrawal'
    Amount REAL NOT NULL,       -- sempre positivo
    Notes TEXT,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (CashSessionId) REFERENCES CashSessions(Id)
);
```

Adicionada via migration em `DatabaseManager.cs` (padrão já existente no projeto).

### Novo modelo `CashMovement` (`Models.cs`)

```csharp
public class CashMovement
{
    public int Id { get; set; }
    public int CashSessionId { get; set; }
    public string Type { get; set; }      // "Deposit" | "Withdrawal"
    public double Amount { get; set; }
    public string? Notes { get; set; }
    public string CreatedAt { get; set; }
}
```

### Novo repositório `CashMovementRepository.cs`

- `CreateAsync(CashMovement movement)` — insere um movimento
- `GetBySessionAsync(int sessionId)` — lista todos os movimentos de uma sessão, ordenados por data
- `GetTotalsAsync(int sessionId)` — devolve `(double totalDeposits, double totalWithdrawals)` calculados via SQL SUM com filtro por Type

A `CashSession` não sofre alterações de schema — os totais de movimentos são sempre calculados em runtime.

---

## Backend — WebBridge

### Novos handlers em `WebBridge.cs`

**`HandleCreateCashMovement(JsonElement payload)`**

- Input: `{ type: "Deposit"|"Withdrawal", amount: number, notes: string }`
- Valida: sessão corrente existe e está aberta; `amount > 0`
- Insere via `CashMovementRepository.CreateAsync()`
- Lança impressão do talão individual em background task (padrão do projeto)
- Output: `{ success: true, movement: { id, type, amount, notes, createdAt } }`
- Em caso de erro: `{ success: false, error: "..." }`

**`HandleGetCashMovements(JsonElement payload)`**

- Input: `{ sessionId: number }`
- Output: `{ movements: [...], totalDeposits: number, totalWithdrawals: number }`

### Registo no dispatcher

```csharp
"createCashMovement" => HandleCreateCashMovement(payload),
"getCashMovements"   => HandleGetCashMovements(payload),
```

### Alteração ao `HandleCloseCashSession`

Antes de fechar a sessão, obtém `(totalDeposits, totalWithdrawals)` via `CashMovementRepository.GetTotalsAsync()` e passa-os ao `ReceiptPrinter.PrintCashSessionReport()`.

O `ClosingBalance` calculado no `CashSessionRepository.CloseAsync()` passa a incluir os movimentos:
```
closingBalance = openingBalance + totalSales + totalDeposits - totalWithdrawals
```

---

## Frontend

### Dropdown no indicador de sessão (`index.html` + `app.js`)

O `#session-indicator` passa a ter comportamento diferenciado:
- **Caixa fechada:** comportamento atual mantido — abre o modal de abertura
- **Caixa aberta:** mostra um dropdown flutuante com:
  - 💰 Depósito → `showCashMovementModal('Deposit')`
  - 💸 Levantamento → `showCashMovementModal('Withdrawal')`
  - *(separador visual)*
  - 🔒 Fechar Caixa → `showCloseSessionModal()`

Clicar fora do dropdown fecha-o (listener `mousedown` no `document`).

### Modal de depósito/levantamento (`app.js`)

Nova função `showCashMovementModal(type)` que reutiliza o `#modal-session` existente:

- **Título:** "Depósito" (azul) ou "Levantamento" (laranja)
- **Campo valor:** input numérico com teclado virtual, valor inicial `0,00`
- **Campo notas:** textarea, placeholder "Nome do operador, motivo..."
- **Botões:** Cancelar | Confirmar

Ao confirmar:
1. `bridge.send('createCashMovement', { type, amount, notes })`
2. Fecha o modal
3. Mostra feedback visual breve (toast ou highlight do indicador)

### Modal de fecho — novos cards (`app.js`)

`showCloseSessionModal()` passa a:
1. Chamar `bridge.send('getCashMovements', { sessionId })` antes de renderizar
2. Adicionar 2 cards ao grid de estatísticas:
   - **Depósitos** — fundo azul, valor em verde, total da sessão
   - **Levantamentos** — fundo laranja escuro, valor em laranja, total da sessão
3. Recalcular o **Total Esperado**:
   ```
   totalEsperado = openingBalance + totalSales + totalDeposits - totalWithdrawals
   ```

Os cards de Depósitos e Levantamentos substituem o card "Transações" para manter o grid 2×2. O count de transações passa para o card de Vendas como subtexto.

---

## Impressão

### Novo método `PrintCashMovementReceipt(CashMovement movement)` (`ReceiptPrinter.cs`)

```
[Cabeçalho — se configurado]
━━━━━━━━━━━━━━━━━━━━━━━━━
        DEPÓSITO           ← tamanho duplo, centrado
━━━━━━━━━━━━━━━━━━━━━━━━━
Data:    03/06/2026 14:32
Valor:          20,00 €    ← bold
Notas: João Silva          ← omitido se Notes vazio
━━━━━━━━━━━━━━━━━━━━━━━━━
[Rodapé — se configurado]
```

Usa o `PrintLayoutConfig` existente — cabeçalho e rodapé são respeitados. "LEVANTAMENTO" para o tipo inverso.

### Alteração ao `PrintCashSessionReport()` (`ReceiptPrinter.cs`)

Assinatura passa a receber `double totalDeposits, double totalWithdrawals`. Insere as linhas de movimentos no bloco de cálculo do saldo, entre `Total Vendas` e `TOTAL CAIXA`:

```
Fundo Caixa:         50,00 €
Total Vendas:       132,50 €
+ Depósitos:        +25,00 €   ← omitido se totalDeposits == 0
− Levantamentos:    −10,00 €   ← omitido se totalWithdrawals == 0
────────────────────────────
TOTAL CAIXA:        197,50 €   ← tamanho duplo
```

---

## Histórico de Sessões

### `history.js` — detalhe de sessão

`renderSessionDetail()` passa a chamar `bridge.send('getCashMovements', { sessionId })` e renderiza uma nova secção "MOVIMENTOS DE CAIXA" abaixo do breakdown de pagamentos:

```
MOVIMENTOS DE CAIXA
  ▲ Depósito      20,00 €   João Silva
  ▼ Levantamento  10,00 €   turno tarde
  ▲ Depósito       5,00 €
```

A secção é omitida se não existirem movimentos na sessão. Sem alterações ao card resumo de sessão na lista.

---

## Ficheiros Alterados / Criados

| Ficheiro | Tipo |
|----------|------|
| `GruderPOS/Data/Models.cs` | Alterado — adiciona `CashMovement` |
| `GruderPOS/Data/DatabaseManager.cs` | Alterado — migration nova tabela |
| `GruderPOS/Data/CashMovementRepository.cs` | **Novo** |
| `GruderPOS/Data/CashSessionRepository.cs` | Alterado — `closingBalance` inclui movimentos |
| `GruderPOS/Bridge/WebBridge.cs` | Alterado — 2 novos handlers + close session |
| `GruderPOS/Printing/ReceiptPrinter.cs` | Alterado — novo método + parâmetros no report |
| `GruderPOS/wwwroot/index.html` | Alterado — dropdown markup |
| `GruderPOS/wwwroot/js/app.js` | Alterado — dropdown, modal, close dialog |
| `GruderPOS/wwwroot/js/history.js` | Alterado — detalhe de sessão |

---

## Fora de Âmbito

- Autenticação/autorização por utilizador (o campo Notas é livre, não validado)
- Edição ou anulação de movimentos após confirmação
- Relatórios agregados de movimentos entre sessões
