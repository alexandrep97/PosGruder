# Virtual Keyboard for POS Touch — Design

**Date:** 2026-05-21  
**Status:** Approved

## Goal

Add a virtual keyboard to all text input fields in GruderPOS, making the application fully usable on a touch-screen POS terminal without a physical keyboard.

## Decisions Made

| Question | Decision |
|---|---|
| Keyboard appearance | Modal dedicado (option B) — consistent with existing modal system |
| Cancel behaviour | Dedicated "Cancelar" button + ✕ header button — both discard changes |
| Scope | All `type="text"` and `textarea` fields across the entire app |
| Numeric fields | Keep existing numpad (unchanged) |
| Trigger | Explicit ⌨️ button next to each text field — no auto-open on focus |

## Architecture

### New file

**`GruderPOS/wwwroot/js/keyboard.js`** — singleton module `virtualKeyboard`

```
virtualKeyboard
  .init()           — registers global click delegation for .btn-keyboard buttons
  .open(el, label)  — opens the modal pre-filled with el's current value
  ._confirm()       — writes buffer back to target input, dispatches 'input' event, closes
  ._cancel()        — discards buffer, closes modal
```

### Modified files

| File | Change |
|---|---|
| `wwwroot/index.html` | Add `#modal-keyboard` HTML; add ⌨️ buttons to static fields; add `<script src="js/keyboard.js">` |
| `wwwroot/css/styles.css` | Add `.input-with-keyboard`, `.btn-keyboard`, `.vk-*` classes |
| `wwwroot/js/settings.js` | Add ⌨️ buttons in all dynamic HTML string templates |
| `wwwroot/js/pos.js` | Add ⌨️ button to generic product description field |

## Fields Receiving ⌨️ Button

| Field | Location | Element ID |
|---|---|---|
| Descrição produto genérico | `index.html` / `pos.js` | `#generic-description` |
| Notas fecho de caixa | `index.html` | `#close-notes` |
| Nome da categoria | `settings.js` | `#cat-name` |
| Nome do produto | `settings.js` | `#prod-name` |
| Recibo — cabeçalho H1 | `settings.js` | `#receipt-h1` |
| Recibo — cabeçalho H2 | `settings.js` | `#receipt-h2` |
| Recibo — cabeçalho H3 | `settings.js` | `#receipt-h3` |
| Recibo — cabeçalho H4 | `settings.js` | `#receipt-h4` |
| Recibo — título corpo | `settings.js` | `#receipt-bt` |
| Recibo — corpo linha 1 | `settings.js` | `#receipt-b1` |
| Recibo — corpo linha 2 | `settings.js` | `#receipt-b2` |
| Recibo — rodapé linha 1 | `settings.js` | `#receipt-f1` |
| Recibo — rodapé linha 2 | `settings.js` | `#receipt-f2` |
| Nome do evento | `settings.js` | `#setting-event` |

**Not included:** `#generic-value`, `#opening-balance`, `#prod-price` (numeric — keep numpad), `#filter-from`, `#filter-to` (date inputs — native date picker).

## Data Flow

```
User taps ⌨️ button
  → button has data-target="field-id" data-label="Field label"
  → virtualKeyboard.open(inputEl, label)
    → copies inputEl.value into internal buffer
    → renders buffer in .vk-display
    → shows #modal-keyboard

User types on virtual keyboard
  → each key appends to buffer
  → ⌫ removes last character
  → ⇧ toggles shift (minúsculas ↔ maiúsculas)
  → !@# toggles symbols layer
  → .vk-display updates live

User taps OK
  → virtualKeyboard._confirm()
    → inputEl.value = buffer
    → inputEl.dispatchEvent(new Event('input'))  ← triggers any dependent listeners
    → closes modal

User taps Cancelar / ✕
  → virtualKeyboard._cancel()
    → buffer discarded
    → closes modal, inputEl unchanged
```

## Keyboard Layout (QWERTY PT)

**Lowercase layer:**
```
[ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ][ 9 ][ 0 ]
[ q ][ w ][ e ][ r ][ t ][ y ][ u ][ i ][ o ][ p ]
  [ a ][ s ][ d ][ f ][ g ][ h ][ j ][ k ][ l ]
[ ⇧ ][ z ][ x ][ c ][ v ][ b ][ n ][ m ][ ⌫ ]
[ !@# ][        espaço        ][ ., ]
```

**Uppercase layer:** same, letters capitalised; ⇧ highlighted in yellow.

**Symbols layer (!@#):**
```
[ ! ][ @ ][ # ][ $ ][ % ][ & ][ * ][ ( ][ ) ][ - ]
[ _ ][ = ][ + ][ [ ][ ] ][ { ][ } ][ / ][ \ ]
  [ | ][ ; ][ : ][ ' ][ " ][ < ][ > ][ ? ]
[ abc ][        espaço        ][ ., ]
```

## CSS Classes

```css
/* Field wrapper */
.input-with-keyboard          /* display: flex; gap: 8px; align-items: center */
.btn-keyboard                 /* 44×44px min; background #333; color #F5C518; border-radius 6px */
.btn-keyboard:active          /* transform: scale(0.95) */

/* Modal */
#modal-keyboard .modal-content  /* max-width: 520px */
.vk-display                   /* preview field: background #333; border 1.5px #F5C518; font-size 18px */
.vk-keyboard                  /* grid container */
.vk-row                       /* flex row; gap 4px; margin-bottom 4px */
.vk-key                       /* min 44px height; background #333; color #fff; border-radius 4px */
.vk-key-wide                  /* flex: 1.5 — shift, backspace */
.vk-key-space                 /* flex: 3 — spacebar */
.vk-key-action                /* background #555 — !@#, ., shift, backspace */
.vk-key:active                /* transform: scale(0.95) */
.vk-key--active               /* background #F5C518; color #1a1a1a — shift ON state */
```

## Modal HTML Structure

```html
<div id="modal-keyboard" class="modal" style="display:none">
  <div class="modal-content">
    <div class="modal-header">
      <span id="vk-label" class="modal-title"></span>
      <button class="modal-close" onclick="virtualKeyboard._cancel()">✕</button>
    </div>
    <div class="modal-body">
      <div id="vk-display" class="vk-display"></div>
      <div id="vk-keyboard" class="vk-keyboard">
        <!-- rows injected by keyboard.js at init -->
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-secondary" onclick="virtualKeyboard._cancel()">Cancelar</button>
      <button class="btn btn-primary" onclick="virtualKeyboard._confirm()">OK — Confirmar</button>
    </div>
  </div>
</div>
```

## ⌨️ Button HTML Pattern

```html
<div class="input-with-keyboard">
  <input type="text" id="cat-name" class="form-input" placeholder="Nome">
  <button class="btn-keyboard" data-target="cat-name" data-label="Nome da categoria">⌨</button>
</div>
```

The same pattern applies to all 14 fields. For `textarea` elements, the wrapper and button are identical; `keyboard.js` handles both `input` and `textarea` targets transparently.

## Out of Scope

- Auto-open on focus (by design — touch targets only)
- Numeric fields (keep existing numpad)
- Date fields (native date picker)
- Multi-language keyboard layouts
- Swipe / gesture input
