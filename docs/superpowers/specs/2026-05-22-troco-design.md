# Design: Calculadora de Troco no Pagamento a Dinheiro

**Data:** 2026-05-22
**Âmbito:** POS GRUDER — `GruderPOS/wwwroot/`

---

## Objetivo

Quando o operador processa um pagamento em dinheiro, opcionalmente mostrar uma modal que pede o valor entregue pelo cliente e calcula o troco automaticamente antes de confirmar a transação. Este comportamento é controlado por uma configuração nas Definições Gerais.

---

## Configuração

### Chave: `ShowChangeCalculator`

- **Tipo:** string `'true'` / `'false'` (padrão: `'false'`)
- **Persistência:** via `bridge.send('saveSettings', { data: { ShowChangeCalculator: '...' } })`
- **Leitura:** disponível em `app.settings.ShowChangeCalculator` após o arranque (carregado em `app.loadSettings()`)

### UI nas Definições Gerais (`settings.renderGeneral`)

Adicionar um toggle switch "Apresentar troco" na secção **Definições Gerais**, posicionado após o campo "Nome do Evento / Festa" e antes da secção de Segurança. O valor é incluído no `saveGeneralSettings` existente:

```js
await bridge.send('saveSettings', {
    data: {
        EventName: eventName,
        ShowChangeCalculator: String(document.getElementById('setting-show-change').checked)
    }
});
// também atualizar app.settings.ShowChangeCalculator
```

O toggle lê o valor atual de `appSettings.ShowChangeCalculator` para determinar o estado inicial (`checked` ou não).

---

## Fluxo de pagamento modificado

### `pos.processPayment()` — intercepção

```
1. Se carrinho vazio → retornar (sem alteração)
2. Se sessão não aberta → mostrar aviso (sem alteração)
3. Se paymentMethod === 'Cash' && app.settings.ShowChangeCalculator === 'true':
   a. Guardar dados em pos._pendingPayment (cashSessionId, totalAmount, paymentMethod, items)
   b. Chamar pos.openChangeModal()
   c. Retornar — NÃO processa ainda
4. Caso contrário → comportamento atual inalterado (processTransaction direto)
```

`pos._pendingPayment` é um objeto temporário em memória, limpo após `confirmCashPayment()` ou `closeChangeModal()`.

### `pos.confirmCashPayment()` — execução do pagamento

Extrai os dados de `pos._pendingPayment` e executa o mesmo bloco que existe hoje em `processPayment()`:
- `bridge.send('processTransaction', { data: ... })`
- `showPaymentSuccess(total)`
- Atualizar `app.currentSession`
- `this.cart = []`, `setPaymentMethod('Cash')`, `renderCart()`, `renderProducts()`
- `closeChangeModal()`

---

## Modal de Troco (`modal-change`)

### Estrutura HTML (estática em `index.html`)

```html
<div id="modal-change" class="modal">
  <div class="modal-content">
    <div class="modal-header">
      <h3>Troco</h3>
    </div>
    <div class="modal-body" style="text-align:center;">
      <!-- Total da conta -->
      <div id="change-total-label">...</div>

      <!-- Valor entregue (read-only, alimentado pelo numpad) -->
      <input type="text" id="change-amount-input" readonly class="form-input form-input-large" ...>

      <!-- Troco calculado -->
      <div id="change-result">...</div>

      <!-- Numpad (idêntico ao de modal-generic) -->
      <div class="numpad">...</div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-secondary" onclick="pos.closeChangeModal()">Cancelar</button>
      <button id="btn-confirm-change" class="btn btn-success" onclick="pos.confirmCashPayment()" disabled>Confirmar</button>
    </div>
  </div>
</div>
```

### Comportamento dinâmico

- **Ao abrir**: `change-amount-input` vazio, troco mostra `---`, botão Confirmar desativado.
- **A cada tecla do numpad**: chama `pos.changeNumpadInput(key)` → atualiza o input → chama `pos.updateChangeDisplay()`.
- **`updateChangeDisplay()`**:
  - `valorEntregue = parseFloat(input.value) || 0`
  - `total = pos._pendingPayment.totalAmount`
  - Se `valorEntregue >= total`: mostra troco `= valorEntregue - total` em verde, ativa botão Confirmar.
  - Se `valorEntregue < total` ou vazio: mostra `---` em cor neutra, desativa botão Confirmar.
- **Cancelar**: fecha modal, limpa `_pendingPayment`, carrinho mantém-se intacto.
- **Confirmar** (só ativo quando `valorEntregue >= total`): executa `confirmCashPayment()`.

### Sem botão X (fechar)

A modal de troco não tem botão X no cabeçalho — só os botões Cancelar/Confirmar — para evitar fechar acidentalmente sem intenção clara. Consistente com a modal de PIN (`modal-pin`).

---

## Novos métodos em `pos.js`

| Método | Responsabilidade |
|---|---|
| `pos.openChangeModal()` | Inicializa modal: escreve total, limpa input, desativa Confirmar, abre modal |
| `pos.closeChangeModal()` | Remove `active` da modal, limpa `_pendingPayment` |
| `pos.changeNumpadInput(key)` | Manipula `#change-amount-input` (igual a `numpadInput` mas target diferente) |
| `pos.updateChangeDisplay()` | Calcula troco, atualiza UI, ativa/desativa botão Confirmar |
| `pos.confirmCashPayment()` | Executa `bridge.send('processTransaction', ...)` com `_pendingPayment`, faz limpeza |

---

## Ficheiros alterados

| Ficheiro | Alteração |
|---|---|
| `wwwroot/index.html` | Adicionar `<div id="modal-change" ...>` |
| `wwwroot/js/pos.js` | Modificar `processPayment()`, adicionar 5 novos métodos, adicionar `_pendingPayment: null` |
| `wwwroot/js/settings.js` | Modificar `renderGeneral()` e `saveGeneralSettings()` |

Nenhum ficheiro novo é criado. Nenhuma alteração ao CSS (reutiliza classes existentes: `.modal`, `.numpad`, `.numpad-btn`, `.form-input-large`, etc.).

---

## O que não muda

- Pagamento com Cartão e MB Way processam imediatamente como antes.
- Se `ShowChangeCalculator === 'false'` (default), o comportamento é idêntico ao atual.
- A estrutura do talão/impressão não é afetada — o troco não é registado na transação.
