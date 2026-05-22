# PIN Security for Settings Access — Design Spec

**Date:** 2026-05-22  
**Status:** Approved

---

## Overview

Add a 4-digit PIN popup that intercepts navigation to the Settings page. Users without a configured PIN access settings freely. Once a PIN is set, every attempt to navigate to settings triggers the PIN modal. The PIN is stored in the backend settings store alongside other app configuration.

---

## Architecture

### New file: `GruderPOS/wwwroot/js/pin.js`

A `pinAuth` object with the following responsibilities:

- `pinAuth.request()` — called by `app.navigate()` before entering settings. Returns a Promise that resolves on correct PIN (or immediately if no PIN is set). Rejects on cancel.
- `pinAuth.open()` — shows the `modal-pin` modal, resets state.
- `pinAuth.close()` — hides the modal, clears entered digits.
- `pinAuth._onDigit(d)` — appends digit, triggers check at 4 digits.
- `pinAuth._check()` — compares entered digits against `app.settings.SettingsPin`. On success: resolves Promise, closes modal. On failure: shake + clear after 800ms.
- `pinAuth._onBackspace()` — removes last digit, updates indicator.
- `pinAuth._updateIndicator()` — updates the 4 dot display (○/●).

PIN is read from `app.settings.SettingsPin` (loaded at app start). After a PIN change in settings, `app.settings.SettingsPin` is updated in memory so verification stays current without a reload.

### Modified: `app.js` — `navigate()`

```js
navigate(page) {
    if (page === 'settings') {
        pinAuth.request().then(() => this._doNavigate('settings'));
        return;
    }
    this._doNavigate(page);
}
```

The existing navigate body moves into `_doNavigate(page)`.

### Modified: `settings.js` — `renderGeneral()`

Adds a "Segurança" section at the bottom of the General tab:

- If no PIN set: single field "Novo PIN" (4 digits) + "Confirmar PIN" + "Definir PIN" button.
- If PIN set: fields "PIN atual", "Novo PIN", "Confirmar PIN" + "Alterar PIN" button + separate "Remover PIN" button.
- Saving calls `bridge.send('saveSettings', { SettingsPin: newPin })` and updates `app.settings.SettingsPin` in memory.
- Removing PIN saves empty string and updates memory.

### Modified: `index.html`

1. Add `modal-pin` markup (static, always present in DOM).
2. Add `<script src="js/pin.js">` before closing `</body>`.

### Modified: `styles.css`

New styles for:
- `.pin-dots` — flex row of 4 dot indicators
- `.pin-dot` — individual dot (○ default, ● filled when digit entered)
- `.pin-numpad` — grid layout for numpad buttons
- `.pin-btn` — individual numpad button (large, touch-friendly, consistent with existing `.btn` style using `--primary`)
- `.pin-error` — error message text (red, small)
- `@keyframes pin-shake` — horizontal shake animation applied to `.pin-dots.shake`

---

## Modal Structure (`modal-pin`)

```html
<div id="modal-pin" class="modal">
  <div class="modal-content">
    <div class="modal-header">
      <h3>Configurações</h3>
    </div>
    <div class="modal-body">
      <p>Introduza o PIN de acesso</p>
      <div class="pin-dots" id="pin-dots">
        <span class="pin-dot"></span>
        <span class="pin-dot"></span>
        <span class="pin-dot"></span>
        <span class="pin-dot"></span>
      </div>
      <p class="pin-error" id="pin-error"></p>
      <div class="pin-numpad">
        <!-- 1-9, 0, backspace -->
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-secondary" onclick="pinAuth.cancel()">Cancelar</button>
    </div>
  </div>
</div>
```

No `✕` close button in the header — cancel is only via the footer button to prevent bypassing the gate.

---

## Behaviour Details

| Scenario | Behaviour |
|---|---|
| No PIN configured | `pinAuth.request()` resolves immediately, settings opens without modal |
| 4th digit entered, PIN correct | Modal closes, settings page opens |
| 4th digit entered, PIN wrong | Dots shake, "PIN incorreto" shown, digits cleared after 800ms |
| Cancel pressed | Modal closes, navigation to settings is aborted |
| PIN not yet set (in General settings) | Only "Novo PIN" + "Confirmar PIN" fields shown |
| PIN already set (in General settings) | "PIN atual" verification required before changing |
| Remove PIN | Saves empty `SettingsPin`, clears from memory |

---

## Files Changed

| File | Change |
|---|---|
| `js/pin.js` | New — `pinAuth` object |
| `js/app.js` | `navigate()` intercepted for settings; body moved to `_doNavigate()` |
| `js/settings.js` | `renderGeneral()` extended with Security section |
| `index.html` | `modal-pin` markup added; `pin.js` script tag added |
| `css/styles.css` | PIN modal styles + shake animation |

---

## Out of Scope

- Attempt lockout / cooldown (not requested)
- PIN hashing (local desktop app, backend is trusted)
- PIN recovery mechanism
