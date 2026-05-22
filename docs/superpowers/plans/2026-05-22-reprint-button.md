# Reprint Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar botão "Reimprimir" nas transações válidas do histórico e nas sessões fechadas da lista de sessões.

**Architecture:** Dois novos handlers no `WebBridge.cs` (um por tipo) expõem as ações `reprintTransaction` e `reprintSession` ao frontend. O `history.js` adiciona os botões às cards existentes e chama esses handlers via `bridge.send()`.

**Tech Stack:** C# (.NET), Vanilla JS, ESC/POS serial printer via `ReceiptPrinter`

---

## Ficheiros Alterados

| Ficheiro | Alteração |
|---|---|
| `GruderPOS/Bridge/WebBridge.cs` | Adicionar 2 casos no switch + 2 métodos handler |
| `GruderPOS/wwwroot/js/history.js` | Adicionar botões HTML + 2 métodos JS |

---

### Task 1: Backend — Handler `reprintTransaction`

**Files:**
- Modify: `GruderPOS/Bridge/WebBridge.cs`

- [ ] **Step 1: Adicionar caso no switch de ações**

Em `WebBridge.cs`, no switch da linha 45, adicionar antes de `"testPrint"`:

```csharp
"reprintTransaction" => await HandleReprintTransaction(root),
"reprintSession"     => await HandleReprintSession(root),
```

O bloco switch fica assim (excerto):

```csharp
"voidTransaction"    => await HandleVoidTransaction(root),
"reprintTransaction" => await HandleReprintTransaction(root),
"reprintSession"     => await HandleReprintSession(root),
"testPrint"          => HandleTestPrint(),
```

- [ ] **Step 2: Implementar `HandleReprintTransaction`**

Adicionar o método a seguir a `HandleVoidTransaction` (linha 277):

```csharp
private async Task<object> HandleReprintTransaction(JsonElement root)
{
    var id = root.GetProperty("id").GetInt32();
    var transaction = await _transactions.GetByIdAsync(id)
        ?? throw new Exception("Transação não encontrada");

    _ = Task.Run(async () =>
    {
        try
        {
            var allSettings = await _settings.GetAllAsync();
            var printConfig = PrintLayoutConfig.FromSettings(allSettings);
            _printer.PrintReceipt(transaction, printConfig);
        }
        catch { }
    });

    return new { reprinted = true };
}
```

- [ ] **Step 3: Implementar `HandleReprintSession`**

Adicionar logo a seguir ao método anterior:

```csharp
private async Task<object> HandleReprintSession(JsonElement root)
{
    var id = root.GetProperty("id").GetInt32();
    var session = await _cashSessions.GetByIdAsync(id)
        ?? throw new Exception("Sessão não encontrada");
    var transactions = await _transactions.GetBySessionAsync(id);

    _ = Task.Run(async () =>
    {
        try
        {
            var allSettings = await _settings.GetAllAsync();
            var printConfig = PrintLayoutConfig.FromSettings(allSettings);
            _printer.PrintCashSessionReport(session, transactions, printConfig);
        }
        catch { }
    });

    return new { reprinted = true };
}
```

- [ ] **Step 4: Verificar que o projeto compila**

```powershell
cd "GruderPOS"
dotnet build
```

Expected: `Build succeeded` sem erros.

- [ ] **Step 5: Commit**

```bash
git add GruderPOS/Bridge/WebBridge.cs
git commit -m "feat: add reprintTransaction and reprintSession bridge handlers"
```

---

### Task 2: Frontend — Botão de reimpressão nas transações

**Files:**
- Modify: `GruderPOS/wwwroot/js/history.js`

- [ ] **Step 1: Adicionar botão "Reimprimir" no bloco `!t.voided`**

Em `history.js`, localizar o bloco `if (!t.voided)` (linha 122). Actualmente:

```javascript
if (!t.voided) {
    html += `
        <div class="transaction-actions">
            <button class="btn btn-danger btn-small" onclick="event.stopPropagation(); history.voidTransaction(${t.id}, this)">Anular</button>
        </div>`;
}
```

Substituir por:

```javascript
if (!t.voided) {
    html += `
        <div class="transaction-actions">
            <button class="btn btn-danger btn-small" onclick="event.stopPropagation(); history.voidTransaction(${t.id}, this)">Anular</button>
            <button class="btn btn-outline btn-small" onclick="event.stopPropagation(); history.reprintTransaction(${t.id}, this)">Reimprimir</button>
        </div>`;
}
```

- [ ] **Step 2: Adicionar método `reprintTransaction`**

A seguir ao método `voidTransaction` (linha 157), adicionar:

```javascript
async reprintTransaction(id, btn) {
    setButtonLoading(btn, true);
    try {
        await bridge.send('reprintTransaction', { id });
        showToast('Talão enviado para impressão', 'success');
    } catch (e) {
        showToast('Erro: ' + e.message, 'error');
    } finally {
        setButtonLoading(btn, false);
    }
},
```

- [ ] **Step 3: Verificar visualmente no browser**

Abrir o histórico de transações, expandir uma transação válida — deve aparecer o botão "Reimprimir" ao lado de "Anular". Transações anuladas não devem ter o botão.

- [ ] **Step 4: Commit**

```bash
git add GruderPOS/wwwroot/js/history.js
git commit -m "feat: add reprint button to valid transactions in history"
```

---

### Task 3: Frontend — Botão de reimpressão nas sessões fechadas

**Files:**
- Modify: `GruderPOS/wwwroot/js/history.js`

- [ ] **Step 1: Adicionar bloco de ações nas sessões fechadas**

Em `renderSessions`, localizar o bloco que fecha a session-card (linha 226). Actualmente:

```javascript
${s.notes ? `<div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border); font-size: 13px; color: var(--text-light);">Notas: ${s.notes}</div>` : ''}
</div>`;
```

Substituir por:

```javascript
${s.notes ? `<div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border); font-size: 13px; color: var(--text-light);">Notas: ${s.notes}</div>` : ''}
${!isOpen ? `
    <div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border);">
        <button class="btn btn-outline btn-small" onclick="history.reprintSession(${s.id}, this)">Reimprimir Relatório</button>
    </div>` : ''}
</div>`;
```

- [ ] **Step 2: Adicionar método `reprintSession`**

A seguir ao método `reprintTransaction` adicionado na Task 2, adicionar:

```javascript
async reprintSession(id, btn) {
    setButtonLoading(btn, true);
    try {
        await bridge.send('reprintSession', { id });
        showToast('Relatório enviado para impressão', 'success');
    } catch (e) {
        showToast('Erro: ' + e.message, 'error');
    } finally {
        setButtonLoading(btn, false);
    }
},
```

- [ ] **Step 3: Verificar visualmente no browser**

Abrir o histórico → separador Sessões. Sessões fechadas devem mostrar o botão "Reimprimir Relatório" no fundo do card. Sessões abertas não devem ter o botão.

- [ ] **Step 4: Commit final**

```bash
git add GruderPOS/wwwroot/js/history.js
git commit -m "feat: add reprint button to closed sessions in history"
```
