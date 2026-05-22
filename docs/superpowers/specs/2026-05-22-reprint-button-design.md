# Design: Botão de Reimpressão no Histórico

**Data:** 2026-05-22

## Objetivo

Adicionar um botão "Reimprimir" nas linhas do histórico de transações e na lista de sessões, permitindo reimprimir talões e relatórios de sessão diretamente a partir do histórico.

## Decisões

- Transações anuladas **não** têm botão de reimpressão.
- Sessões abertas **não** têm botão de reimpressão (só sessões fechadas).
- O layout de impressão usa a configuração existente (`PrintLayoutConfig.FromSettings()`).

## Alterações Frontend (`GruderPOS/wwwroot/js/history.js`)

### Transações

No bloco `if (!t.voided)` (linha 122), adicionar um botão "Reimprimir" ao lado do botão "Anular" existente em `.transaction-actions`:

```html
<button class="btn btn-outline btn-small" onclick="event.stopPropagation(); history.reprintTransaction(${t.id}, this)">Reimprimir</button>
```

Novo método `reprintTransaction(id, btn)`:
- Chama `bridge.send('reprintTransaction', { id })`
- Aplica loading state ao botão durante a chamada
- Mostra toast de sucesso ou erro

### Sessões

Para sessões `!isOpen`, adicionar bloco `.session-actions` com botão "Reimprimir Relatório":

```html
<div class="session-actions" style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border);">
    <button class="btn btn-outline btn-small" onclick="history.reprintSession(${s.id}, this)">Reimprimir Relatório</button>
</div>
```

Novo método `reprintSession(id, btn)`:
- Chama `bridge.send('reprintSession', { id })`
- Aplica loading state ao botão durante a chamada
- Mostra toast de sucesso ou erro

## Alterações Backend (`GruderPOS/Bridge/WebBridge.cs`)

### Switch de ações

Adicionar dois novos casos:

```csharp
"reprintTransaction" => await HandleReprintTransaction(root),
"reprintSession"     => await HandleReprintSession(root),
```

### `HandleReprintTransaction`

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

### `HandleReprintSession`

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

## Ficheiros Alterados

| Ficheiro | Tipo de Alteração |
|---|---|
| `GruderPOS/wwwroot/js/history.js` | Adicionar botões HTML + 2 métodos JS |
| `GruderPOS/Bridge/WebBridge.cs` | Adicionar 2 casos no switch + 2 handlers |
