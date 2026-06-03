# Cash Movements — Depósitos e Levantamentos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deposit/withdrawal operations to open cash sessions, reflected in the close dialog, closing receipt, own printed receipt, and session history.

**Architecture:** New `CashMovements` SQLite table isolated from `Transactions`. A new `CashMovementRepository` handles CRUD. `WebBridge` gains two actions (`createCashMovement`, `getCashMovements`) and updates `closeCashSession`/`reprintSession` to include movement totals. Frontend adds a dropdown on the session indicator, a movement modal, and updates the close dialog and history.

**Tech Stack:** C# / .NET 8 WinForms, SQLite + Dapper, ESC/POS thermal printing, vanilla JavaScript

---

## File Map

| File | Change |
|------|--------|
| `GruderPOS/Data/Models.cs` | Add `CashMovement` class; extend `CashSessionDetail` |
| `GruderPOS/Data/DatabaseManager.cs` | Add `CashMovements` table migration |
| `GruderPOS/Data/CashMovementRepository.cs` | **New** — `CreateAsync`, `GetBySessionAsync`, `GetTotalsAsync`, `GetAllAsync` |
| `GruderPOS/Data/CashSessionRepository.cs` | Update `CloseAsync` to accept and apply movement totals |
| `GruderPOS/Bridge/WebBridge.cs` | Inject repo; add `createCashMovement`, `getCashMovements`; update `closeCashSession`, `reprintSession` |
| `GruderPOS/Printing/ReceiptPrinter.cs` | Add `PrintCashMovementReceipt`; update `PrintCashSessionReport` signature |
| `GruderPOS/wwwroot/index.html` | Add `#session-dropdown` markup |
| `GruderPOS/wwwroot/css/styles.css` | Add dropdown + new stat card styles |
| `GruderPOS/wwwroot/js/app.js` | Dropdown logic, `showCashMovementModal`, `submitCashMovement`, async `showCloseSessionModal` |
| `GruderPOS/wwwroot/js/history.js` | Show movements section in session cards |

---

## Task 1: Data Layer — Model, Migration, Repository

**Files:**
- Modify: `GruderPOS/Data/Models.cs`
- Modify: `GruderPOS/Data/DatabaseManager.cs`
- Create: `GruderPOS/Data/CashMovementRepository.cs`

- [ ] **Step 1: Add `CashMovement` model and extend `CashSessionDetail` in `Models.cs`**

  Append after the `CashSessionDetail` class (after line 51):

  ```csharp
  public class CashMovement
  {
      public int Id { get; set; }
      public int CashSessionId { get; set; }
      public string Type { get; set; } = string.Empty;   // "Deposit" | "Withdrawal"
      public double Amount { get; set; }
      public string? Notes { get; set; }
      public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
  }
  ```

  `CashSessionDetail` does **not** need changes — the history page fetches movements separately via `getAllCashMovements`.

- [ ] **Step 2: Add `CashMovements` table migration to `DatabaseManager.cs`**

  Add after the `AppSettings` CREATE TABLE block (after line 104), before the `// Seed default settings` comment:

  ```csharp
  connection.Execute(@"
      CREATE TABLE IF NOT EXISTS CashMovements (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          CashSessionId INTEGER NOT NULL,
          Type TEXT NOT NULL,
          Amount REAL NOT NULL,
          Notes TEXT,
          CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
          FOREIGN KEY (CashSessionId) REFERENCES CashSessions(Id)
      );
  ");
  ```

- [ ] **Step 3: Create `GruderPOS/Data/CashMovementRepository.cs`**

  ```csharp
  using Dapper;

  namespace GruderPOS.Data;

  public class CashMovementRepository
  {
      private readonly DatabaseManager _db;

      public CashMovementRepository(DatabaseManager db) => _db = db;

      public async Task<CashMovement> CreateAsync(CashMovement movement)
      {
          using var conn = _db.GetConnection();
          movement.CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
          var id = await conn.ExecuteScalarAsync<int>(@"
              INSERT INTO CashMovements (CashSessionId, Type, Amount, Notes, CreatedAt)
              VALUES (@CashSessionId, @Type, @Amount, @Notes, @CreatedAt);
              SELECT last_insert_rowid();", movement);
          movement.Id = id;
          return movement;
      }

      public async Task<IEnumerable<CashMovement>> GetBySessionAsync(int sessionId)
      {
          using var conn = _db.GetConnection();
          return await conn.QueryAsync<CashMovement>(
              "SELECT * FROM CashMovements WHERE CashSessionId = @Id ORDER BY CreatedAt",
              new { Id = sessionId });
      }

      public async Task<(double TotalDeposits, double TotalWithdrawals)> GetTotalsAsync(int sessionId)
      {
          using var conn = _db.GetConnection();
          var deposits = await conn.ExecuteScalarAsync<double>(
              "SELECT COALESCE(SUM(Amount), 0) FROM CashMovements WHERE CashSessionId = @Id AND Type = 'Deposit'",
              new { Id = sessionId });
          var withdrawals = await conn.ExecuteScalarAsync<double>(
              "SELECT COALESCE(SUM(Amount), 0) FROM CashMovements WHERE CashSessionId = @Id AND Type = 'Withdrawal'",
              new { Id = sessionId });
          return (deposits, withdrawals);
      }

      public async Task<IEnumerable<CashMovement>> GetAllAsync()
      {
          using var conn = _db.GetConnection();
          return await conn.QueryAsync<CashMovement>(
              "SELECT * FROM CashMovements ORDER BY CreatedAt");
      }
  }
  ```

- [ ] **Step 4: Build to verify compilation**

  Run from `GruderPOS/` directory:
  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

  ```bash
  git add GruderPOS/Data/Models.cs GruderPOS/Data/DatabaseManager.cs GruderPOS/Data/CashMovementRepository.cs
  git commit -m "feat: add CashMovement model, DB migration, and repository"
  ```

---

## Task 2: Update `CashSessionRepository.CloseAsync`

**Files:**
- Modify: `GruderPOS/Data/CashSessionRepository.cs`

- [ ] **Step 1: Update `CloseAsync` signature and closing balance calculation**

  Replace the current `CloseAsync` method (lines 36–57) with:

  ```csharp
  public async Task<CashSession?> CloseAsync(string? notes, double totalDeposits = 0, double totalWithdrawals = 0)
  {
      using var conn = _db.GetConnection();
      var session = await conn.QueryFirstOrDefaultAsync<CashSession>(
          "SELECT * FROM CashSessions WHERE Status = 'Open' ORDER BY OpenedAt DESC LIMIT 1");

      if (session == null) return null;

      var closingBalance = session.OpeningBalance + session.TotalSales + totalDeposits - totalWithdrawals;

      await conn.ExecuteAsync(@"
          UPDATE CashSessions SET 
              Status = 'Closed', 
              ClosedAt = datetime('now','localtime'),
              ClosingBalance = @ClosingBalance,
              Notes = @Notes
          WHERE Id = @Id",
          new { ClosingBalance = closingBalance, Notes = notes, Id = session.Id });

      return await conn.QueryFirstAsync<CashSession>(
          "SELECT * FROM CashSessions WHERE Id = @Id", new { Id = session.Id });
  }
  ```

- [ ] **Step 2: Build**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

  ```bash
  git add GruderPOS/Data/CashSessionRepository.cs
  git commit -m "feat: include movement totals in closing balance calculation"
  ```

---

## Task 3: Update `ReceiptPrinter`

**Files:**
- Modify: `GruderPOS/Printing/ReceiptPrinter.cs`

- [ ] **Step 1: Update `PrintCashSessionReport` signature and insert movement lines**

  Change the method signature from:
  ```csharp
  public bool PrintCashSessionReport(CashSession session, IEnumerable<Transaction> transactions, PrintLayoutConfig config)
  ```
  to:
  ```csharp
  public bool PrintCashSessionReport(CashSession session, IEnumerable<Transaction> transactions, PrintLayoutConfig config, double totalDeposits = 0, double totalWithdrawals = 0)
  ```

  Then replace the totals summary block (the lines around `TOTAL VENDAS` and `TOTAL CAIXA`, approximately lines 449–456) with:

  ```csharp
  // Totals summary
  PrintLine('-');
  _serial.Write(EscPosCommands.BoldOn);
  _serial.WriteText(FormatTotalLine("TOTAL VENDAS:", $"{session.TotalSales:F2}"));
  if (totalDeposits > 0)
      _serial.WriteText(FormatTotalLine("+ Depositos:", $"+{totalDeposits:F2}"));
  if (totalWithdrawals > 0)
      _serial.WriteText(FormatTotalLine("- Levantamentos:", $"-{totalWithdrawals:F2}"));
  PrintLine('-');
  var closing = session.ClosingBalance ?? (session.OpeningBalance + session.TotalSales + totalDeposits - totalWithdrawals);
  _serial.Write(EscPosCommands.SizeDoubleHeight);
  _serial.WriteText(FormatTotalLine("TOTAL CAIXA:", $"{closing:F2} EUR"));
  _serial.Write(EscPosCommands.SizeNormal);
  _serial.Write(EscPosCommands.BoldOff);
  ```

- [ ] **Step 2: Add `PrintCashMovementReceipt` method**

  Insert the following method before `PrintTest()`:

  ```csharp
  public bool PrintCashMovementReceipt(CashMovement movement, PrintLayoutConfig config)
  {
      try
      {
          if (!_serial.Connect()) return false;

          InitPrinter();

          _serial.Write(EscPosCommands.AlignCenter);
          _serial.Write(EscPosCommands.BoldOn);
          _serial.Write(EscPosCommands.SizeDouble);
          if (config.HeaderEnabled && !string.IsNullOrWhiteSpace(config.HeaderLine1))
              _serial.WriteText($"{config.HeaderLine1}\n");
          else
              _serial.WriteText("GRUDER\n");
          _serial.Write(EscPosCommands.SizeNormal);
          _serial.Write(EscPosCommands.BoldOff);
          PrintLine('=');

          _serial.Write(EscPosCommands.AlignCenter);
          _serial.Write(EscPosCommands.BoldOn);
          _serial.Write(EscPosCommands.SizeDouble);
          var typeLabel = movement.Type == "Deposit" ? "DEPOSITO" : "LEVANTAMENTO";
          _serial.WriteText($"{typeLabel}\n");
          _serial.Write(EscPosCommands.SizeNormal);
          _serial.Write(EscPosCommands.BoldOff);
          PrintLine('=');

          _serial.Write(EscPosCommands.AlignLeft);
          if (DateTime.TryParse(movement.CreatedAt, out var dt))
              _serial.WriteText($"Data:  {dt:dd/MM/yyyy HH:mm}\n");
          _serial.Write(EscPosCommands.BoldOn);
          _serial.WriteText(FormatTotalLine("Valor:", $"{movement.Amount:F2} EUR"));
          _serial.Write(EscPosCommands.BoldOff);

          if (!string.IsNullOrWhiteSpace(movement.Notes))
          {
              PrintLine('-');
              _serial.WriteText($"Notas: {movement.Notes}\n");
          }

          PrintLine('=');

          if (config.FooterEnabled)
              PrintFooter(config);

          _serial.Write(EscPosCommands.FeedLines(4));
          _serial.Write(EscPosCommands.PartialCut);

          return true;
      }
      catch (Exception ex)
      {
          System.Diagnostics.Debug.WriteLine($"Print movement receipt error: {ex.Message}");
          return false;
      }
  }
  ```

- [ ] **Step 3: Build**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add GruderPOS/Printing/ReceiptPrinter.cs
  git commit -m "feat: add PrintCashMovementReceipt and update PrintCashSessionReport with movement lines"
  ```

---

## Task 4: Update `WebBridge`

**Files:**
- Modify: `GruderPOS/Bridge/WebBridge.cs`

- [ ] **Step 1: Inject `CashMovementRepository` into `WebBridge`**

  Add field after `_cashSessions` (line 13):
  ```csharp
  private readonly CashMovementRepository _cashMovements;
  ```

  Add initialization in the constructor after `_cashSessions = new CashSessionRepository(db);` (line 31):
  ```csharp
  _cashMovements = new CashMovementRepository(db);
  ```

- [ ] **Step 2: Register new actions in the switch expression**

  Add two entries in the `action switch` block (after the `"reprintSession"` line, before `"testPrint"`):
  ```csharp
  "createCashMovement" => await HandleCreateCashMovement(root),
  "getCashMovements"   => await HandleGetCashMovements(root),
  "getAllCashMovements" => await HandleGetAllCashMovements(),
  ```

- [ ] **Step 3: Update `HandleCloseCashSession` to fetch movement totals**

  Replace the current `HandleCloseCashSession` method with:

  ```csharp
  private async Task<object> HandleCloseCashSession(JsonElement root)
  {
      var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;

      var currentSession = await _cashSessions.GetCurrentAsync();
      if (currentSession == null) return new { error = "No open session" };

      var (totalDeposits, totalWithdrawals) = await _cashMovements.GetTotalsAsync(currentSession.Id);
      var session = await _cashSessions.CloseAsync(notes, totalDeposits, totalWithdrawals);

      if (session != null)
      {
          var transactions = await _transactions.GetBySessionAsync(session.Id);
          var deps = totalDeposits;
          var withs = totalWithdrawals;
          _ = Task.Run(async () =>
          {
              try
              {
                  var allSettings = await _settings.GetAllAsync();
                  var printConfig = PrintLayoutConfig.FromSettings(allSettings);
                  _printer.PrintCashSessionReport(session, transactions, printConfig, deps, withs);
              }
              catch { }
          });
      }

      return session ?? (object)new { error = "No open session" };
  }
  ```

- [ ] **Step 4: Update `HandleReprintSession` to include movement totals**

  Replace the current `HandleReprintSession` method with:

  ```csharp
  private async Task<object> HandleReprintSession(JsonElement root)
  {
      var id = root.GetProperty("id").GetInt32();
      var session = await _cashSessions.GetByIdAsync(id)
          ?? throw new Exception("Sessão não encontrada");
      var transactions = await _transactions.GetBySessionAsync(id);
      var (totalDeposits, totalWithdrawals) = await _cashMovements.GetTotalsAsync(id);
      var deps = totalDeposits;
      var withs = totalWithdrawals;

      _ = Task.Run(async () =>
      {
          try
          {
              var allSettings = await _settings.GetAllAsync();
              var printConfig = PrintLayoutConfig.FromSettings(allSettings);
              _printer.PrintCashSessionReport(session, transactions, printConfig, deps, withs);
          }
          catch { }
      });

      return new { reprinted = true };
  }
  ```

- [ ] **Step 5: Add the three new handler methods**

  Add these methods before the `// Printer` section:

  ```csharp
  // Cash Movements
  private async Task<object> HandleCreateCashMovement(JsonElement root)
  {
      var currentSession = await _cashSessions.GetCurrentAsync()
          ?? throw new Exception("Sem sessão de caixa aberta");

      var type = root.GetProperty("type").GetString() ?? "";
      if (type != "Deposit" && type != "Withdrawal")
          throw new Exception("Tipo de movimento inválido");

      var amount = root.GetProperty("amount").GetDouble();
      if (amount <= 0)
          throw new Exception("O valor tem de ser maior que zero");

      var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;

      var movement = await _cashMovements.CreateAsync(new CashMovement
      {
          CashSessionId = currentSession.Id,
          Type = type,
          Amount = amount,
          Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
      });

      _ = Task.Run(async () =>
      {
          try
          {
              var allSettings = await _settings.GetAllAsync();
              var printConfig = PrintLayoutConfig.FromSettings(allSettings);
              _printer.PrintCashMovementReceipt(movement, printConfig);
          }
          catch { }
      });

      return movement;
  }

  private async Task<object> HandleGetCashMovements(JsonElement root)
  {
      var sessionId = root.GetProperty("sessionId").GetInt32();
      var movements = await _cashMovements.GetBySessionAsync(sessionId);
      var (totalDeposits, totalWithdrawals) = await _cashMovements.GetTotalsAsync(sessionId);
      return new { movements, totalDeposits, totalWithdrawals };
  }

  private async Task<object> HandleGetAllCashMovements()
  {
      return await _cashMovements.GetAllAsync();
  }
  ```

- [ ] **Step 6: Build**

  ```
  dotnet build
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

  ```bash
  git add GruderPOS/Bridge/WebBridge.cs
  git commit -m "feat: add cash movement bridge handlers and update close/reprint with movement totals"
  ```

---

## Task 5: Frontend HTML & CSS — Dropdown Markup and Styles

**Files:**
- Modify: `GruderPOS/wwwroot/index.html`
- Modify: `GruderPOS/wwwroot/css/styles.css`

- [ ] **Step 1: Add dropdown markup in `index.html`**

  The sidebar footer currently (lines 29–34):
  ```html
  <div class="sidebar-footer">
      <div id="session-indicator" class="session-closed">
          <span class="session-dot"></span>
          <span id="session-text">Caixa Fechada</span>
      </div>
  </div>
  ```

  Replace with:
  ```html
  <div class="sidebar-footer">
      <div id="session-dropdown" class="session-dropdown hidden">
          <button class="session-dropdown-item" onclick="app.showCashMovementModal('Deposit')">💰 Depósito</button>
          <button class="session-dropdown-item" onclick="app.showCashMovementModal('Withdrawal')">💸 Levantamento</button>
          <div class="session-dropdown-divider"></div>
          <button class="session-dropdown-item session-dropdown-danger" onclick="app.hideSessionDropdown(); app.showCloseSessionModal()">🔒 Fechar Caixa</button>
      </div>
      <div id="session-indicator" class="session-closed">
          <span class="session-dot"></span>
          <span id="session-text">Caixa Fechada</span>
      </div>
  </div>
  ```

- [ ] **Step 2: Add CSS for dropdown and updated stat cards in `styles.css`**

  Append at the end of `styles.css`:

  ```css
  /* Session Dropdown */
  .sidebar-footer {
      position: relative;
  }
  .session-dropdown {
      position: absolute;
      bottom: calc(100% + 4px);
      left: 0;
      right: 0;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 4px;
      z-index: 200;
      box-shadow: 0 -4px 12px rgba(0,0,0,0.3);
  }
  .session-dropdown.hidden { display: none; }
  .session-dropdown-item {
      display: block;
      width: 100%;
      padding: 9px 12px;
      background: none;
      border: none;
      color: var(--text);
      text-align: left;
      cursor: pointer;
      border-radius: 4px;
      font-size: 13px;
  }
  .session-dropdown-item:hover { background: var(--hover); }
  .session-dropdown-divider { height: 1px; background: var(--border); margin: 4px 2px; }
  .session-dropdown-danger { color: var(--danger); }

  /* Close dialog — movement stat cards */
  .session-stat-deposits {
      border-top: 2px solid #2196f3 !important;
  }
  .session-stat-withdrawals {
      border-top: 2px solid #ff9800 !important;
  }
  .session-stat-total-row {
      grid-column: 1 / -1;
      border-top: 2px solid var(--success) !important;
  }
  ```

- [ ] **Step 3: Verify the app loads without JS errors**

  Run the app (`dotnet run` in `GruderPOS/` or launch via Visual Studio). Open the POS page. Check browser devtools console — no errors expected.

- [ ] **Step 4: Commit**

  ```bash
  git add GruderPOS/wwwroot/index.html GruderPOS/wwwroot/css/styles.css
  git commit -m "feat: add session dropdown markup and styles"
  ```

---

## Task 6: Frontend `app.js` — Dropdown, Movement Modal, Close Dialog

**Files:**
- Modify: `GruderPOS/wwwroot/js/app.js`

- [ ] **Step 1: Replace the session indicator click handler in `init()`**

  Current handler (lines 14–19):
  ```javascript
  document.getElementById('session-indicator').onclick = () => {
      if (this.currentSession && this.currentSession.id) {
          this.showCloseSessionModal();
      } else {
          this.showOpenSessionModal();
      }
  };
  ```

  Replace with:
  ```javascript
  document.getElementById('session-indicator').onclick = () => {
      if (this.currentSession && this.currentSession.id) {
          this.showSessionDropdown();
      } else {
          this.showOpenSessionModal();
      }
  };
  ```

- [ ] **Step 2: Add `showSessionDropdown` and `hideSessionDropdown` methods**

  Add after the `closeSessionModal` method (after line 149):

  ```javascript
  showSessionDropdown() {
      document.getElementById('session-dropdown').classList.remove('hidden');
      const handler = (e) => {
          if (!e.target.closest('#session-dropdown') && !e.target.closest('#session-indicator')) {
              this.hideSessionDropdown();
          }
      };
      setTimeout(() => document.addEventListener('mousedown', handler, { once: true }), 0);
  },

  hideSessionDropdown() {
      document.getElementById('session-dropdown').classList.add('hidden');
  },
  ```

- [ ] **Step 3: Add `showCashMovementModal` method**

  Add after `hideSessionDropdown`:

  ```javascript
  showCashMovementModal(type) {
      this.hideSessionDropdown();
      const modal = document.getElementById('modal-session');
      const title = document.getElementById('session-modal-title');
      const body = document.getElementById('session-modal-body');
      const footer = document.getElementById('session-modal-footer');

      const isDeposit = type === 'Deposit';
      const color = isDeposit ? '#2196f3' : '#ff9800';
      const label = isDeposit ? '💰 Depósito' : '💸 Levantamento';
      const btnClass = isDeposit ? 'btn-success' : 'btn-danger';

      title.innerHTML = `<span style="color:${color}">${label}</span>`;
      body.innerHTML = `
          <div class="form-group">
              <label>Valor (€)</label>
              <input type="number" id="movement-amount" class="form-input form-input-large"
                     value="0.00" step="0.01" min="0.01">
          </div>
          <div class="form-group">
              <label>Notas</label>
              <div class="input-with-keyboard">
                  <textarea id="movement-notes" class="form-input" rows="3"
                            placeholder="Nome do operador, motivo..."></textarea>
                  <button class="btn-keyboard" data-target="movement-notes" data-label="Notas">⌨</button>
              </div>
          </div>
      `;
      footer.innerHTML = `
          <button class="btn btn-secondary" onclick="app.closeSessionModal()">Cancelar</button>
          <button class="btn ${btnClass}" onclick="app.submitCashMovement('${type}')">Confirmar</button>
      `;
      modal.classList.add('active');
      const amountInput = document.getElementById('movement-amount');
      amountInput.focus();
      amountInput.select();
  },
  ```

- [ ] **Step 4: Add `submitCashMovement` method**

  Add after `showCashMovementModal`:

  ```javascript
  async submitCashMovement(type) {
      const amount = parseFloat(document.getElementById('movement-amount').value) || 0;
      if (amount <= 0) {
          showToast('O valor tem de ser maior que zero', 'error');
          return;
      }
      const notes = document.getElementById('movement-notes').value;
      const btn = document.querySelector('#session-modal-footer .btn:last-child');
      setButtonLoading(btn, true);
      try {
          await bridge.send('createCashMovement', { type, amount, notes });
          this.closeSessionModal();
          const label = type === 'Deposit' ? 'Depósito registado!' : 'Levantamento registado!';
          showToast(label, 'success');
      } catch (e) {
          showToast('Erro: ' + e.message, 'error');
          setButtonLoading(btn, false);
      }
  },
  ```

- [ ] **Step 5: Replace `showCloseSessionModal` with async version that fetches movement totals**

  Replace the current `showCloseSessionModal` method (lines 101–145) with:

  ```javascript
  async showCloseSessionModal() {
      const modal = document.getElementById('modal-session');
      const title = document.getElementById('session-modal-title');
      const body = document.getElementById('session-modal-body');
      const footer = document.getElementById('session-modal-footer');

      const s = this.currentSession;

      let totalDeposits = 0, totalWithdrawals = 0;
      try {
          const movData = await bridge.send('getCashMovements', { sessionId: s.id });
          totalDeposits = movData.totalDeposits || 0;
          totalWithdrawals = movData.totalWithdrawals || 0;
      } catch (e) { /* continue without movements if fetch fails */ }

      const expectedBalance = (s.openingBalance || 0) + (s.totalSales || 0) + totalDeposits - totalWithdrawals;

      title.textContent = 'Fechar Caixa';
      body.innerHTML = `
          <div style="margin-bottom: 20px;">
              <div class="session-card-stats" style="margin-bottom: 16px;">
                  <div class="session-stat">
                      <div class="session-stat-value">${formatCurrency(s.openingBalance || 0)}</div>
                      <div class="session-stat-label">Fundo Caixa</div>
                  </div>
                  <div class="session-stat">
                      <div class="session-stat-value">${formatCurrency(s.totalSales || 0)}</div>
                      <div class="session-stat-label">Vendas (${s.totalTransactions || 0})</div>
                  </div>
                  <div class="session-stat session-stat-deposits">
                      <div class="session-stat-value" style="color:#4caf50">+${formatCurrency(totalDeposits)}</div>
                      <div class="session-stat-label">Depósitos</div>
                  </div>
                  <div class="session-stat session-stat-withdrawals">
                      <div class="session-stat-value" style="color:#ff9800">-${formatCurrency(totalWithdrawals)}</div>
                      <div class="session-stat-label">Levantamentos</div>
                  </div>
              </div>
              <div class="session-stat session-stat-total-row" style="padding:10px;border-radius:6px;background:var(--surface);text-align:center">
                  <div class="session-stat-value text-success" style="font-size:1.4rem">${formatCurrency(expectedBalance)}</div>
                  <div class="session-stat-label">Total Esperado</div>
              </div>
          </div>
          <div class="form-group">
              <label>Notas (opcional)</label>
              <div class="input-with-keyboard">
                  <textarea id="close-notes" class="form-input" rows="3"
                            placeholder="Observações sobre o fecho de caixa..."></textarea>
                  <button class="btn-keyboard" data-target="close-notes" data-label="Notas">⌨</button>
              </div>
          </div>
      `;
      footer.innerHTML = `
          <button class="btn btn-secondary" onclick="app.closeSessionModal()">Cancelar</button>
          <button class="btn btn-danger" onclick="app.closeSession()">Fechar Caixa</button>
      `;
      modal.classList.add('active');
  },
  ```

- [ ] **Step 6: Manual test — Dropdown and movement modal**

  Launch the app. With a cash session open:
  1. Click "Caixa Aberta" indicator → dropdown should appear with 3 options
  2. Click outside → dropdown should close
  3. Click "Depósito" → modal opens with blue title, amount and notes fields
  4. Enter amount 0 → click Confirmar → toast "O valor tem de ser maior que zero"
  5. Enter amount 20, notes "João Silva" → Confirmar → toast "Depósito registado!" and movement receipt prints
  6. Click "Levantamento" → modal opens with orange title
  7. Enter amount 10 → Confirmar → toast "Levantamento registado!"
  8. Click "Fechar Caixa" → close session modal opens showing Depósitos +20,00€ and Levantamentos -10,00€

- [ ] **Step 7: Commit**

  ```bash
  git add GruderPOS/wwwroot/js/app.js
  git commit -m "feat: add session dropdown, movement modal, and async close dialog with movement cards"
  ```

---

## Task 7: Frontend `history.js` — Movements in Session Detail

**Files:**
- Modify: `GruderPOS/wwwroot/js/history.js`

- [ ] **Step 1: Update `renderSessions` to fetch and display movements**

  In `renderSessions`, the sessions are fetched via `bridge.send('getCashSessions')`. Add a second fetch for all movements before the render loop.

  Replace (in `renderSessions`, after `this.sessions = rawSessions || [];`):

  Current code at the top of `renderSessions` after the try/catch:
  ```javascript
  this.sessions = rawSessions || [];
  ```

  Replace with:
  ```javascript
  this.sessions = rawSessions || [];

  let allMovements = [];
  try {
      allMovements = await bridge.send('getAllCashMovements');
  } catch (e) { /* continue without movements */ }

  const movementsBySession = {};
  (allMovements || []).forEach(m => {
      if (!movementsBySession[m.cashSessionId]) movementsBySession[m.cashSessionId] = [];
      movementsBySession[m.cashSessionId].push(m);
  });
  ```

- [ ] **Step 2: Add movements section HTML inside the session card render loop**

  In the `renderSessions` forEach loop, after `${breakdownHtml}` and before the `session-totals` div, add:

  ```javascript
  const sessionMovements = movementsBySession[s.id] || [];
  let movementsHtml = '';
  if (sessionMovements.length > 0) {
      movementsHtml = `<div class="session-payment-breakdown">
          <div class="session-breakdown-label">Movimentos de Caixa</div>`;
      sessionMovements.forEach(m => {
          const isDeposit = m.type === 'Deposit';
          const sign = isDeposit ? '▲' : '▼';
          const color = isDeposit ? 'color:#4caf50' : 'color:#ff9800';
          const typeLabel = isDeposit ? 'Depósito' : 'Levantamento';
          movementsHtml += `<div class="session-breakdown-row">
              <span style="${color}">${sign} ${typeLabel}</span>
              <span style="display:flex;flex-direction:column;align-items:flex-end">
                  <span style="${color}">${isDeposit ? '+' : '-'}${formatCurrency(m.amount)}</span>
                  ${m.notes ? `<span style="font-size:11px;color:var(--text-muted)">${m.notes}</span>` : ''}
              </span>
          </div>`;
      });
      movementsHtml += `</div>`;
  }
  ```

  Then change the template literal to include `${movementsHtml}` after `${breakdownHtml}`:
  ```javascript
  ${breakdownHtml}
  ${movementsHtml}
  ```

- [ ] **Step 3: Manual test — History session view**

  Launch the app. Navigate to Histórico → Sessões. Open a session that had deposits/withdrawals:
  - The session card should show a "Movimentos de Caixa" section
  - Deposits show in green with ▲ and notes
  - Withdrawals show in orange with ▼ and notes
  - Sessions with no movements show no section

- [ ] **Step 4: Commit**

  ```bash
  git add GruderPOS/wwwroot/js/history.js
  git commit -m "feat: show cash movements section in session history"
  ```

---

## Task 8: End-to-End Manual Verification

- [ ] **Step 1: Full flow test**

  1. Open cash session with €50 fundo
  2. Process 2 transactions (any method, any amount)
  3. Register a deposit of €20 with notes "João Silva"
     - Verify: receipt prints with "DEPOSITO", €20,00, "João Silva"
  4. Register a withdrawal of €10 with no notes
     - Verify: receipt prints with "LEVANTAMENTO", €10,00, no notes line
  5. Click Fechar Caixa via dropdown
     - Verify close dialog shows: Fundo €50, Vendas (2), Depósitos +€20,00, Levantamentos -€10,00
     - Verify Total Esperado = €50 + vendas + €20 - €10
  6. Close the session
     - Verify: closing receipt prints with `+ Depositos: +20,00` and `- Levantamentos: -10,00` lines
     - Verify: TOTAL CAIXA = correct sum
  7. Navigate to Histórico → Sessões
     - Verify: closed session shows "Movimentos de Caixa" section with both movements
  8. Click "Reimprimir Relatório" on the closed session
     - Verify: report reprints with correct movement totals

- [ ] **Step 2: Final commit**

  ```bash
  git add -A
  git commit -m "feat: cash movements (deposits/withdrawals) — complete implementation"
  ```
