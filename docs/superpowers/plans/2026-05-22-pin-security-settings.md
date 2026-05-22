# PIN Security for Settings Access — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Block access to the Settings page behind a 4-digit PIN popup with a touch-friendly numpad, stored in backend settings, configurable from within General settings.

**Architecture:** A new `pinAuth` object in `pin.js` intercepts `app.navigate('settings')` by wrapping the existing body into `_doNavigate()` and guarding it with a Promise-based PIN modal. The PIN is read from `app.settings.SettingsPin` (loaded at startup). A Security section added to the General settings tab allows setting, changing, and removing the PIN.

**Tech Stack:** Vanilla JS, HTML5, CSS3 (no frameworks, no build step — matches existing codebase)

---

### Task 1: Add PIN styles to `styles.css`

**Files:**
- Modify: `GruderPOS/wwwroot/css/styles.css` — after line 844 (after `.numpad-delete` block, before `/* ===== Forms =====*/`)

- [ ] **Step 1: Add CSS for PIN dots, shake animation, and error text**

Open `GruderPOS/wwwroot/css/styles.css`. Insert the following block between the `.numpad-delete` rule (line 842) and the `/* ===== Forms =====*/` comment (line 846):

```css
/* ===== PIN Modal ===== */
.pin-dots {
    display: flex;
    justify-content: center;
    gap: 16px;
    margin: 8px 0 16px;
}

.pin-dot {
    width: 20px;
    height: 20px;
    border-radius: 50%;
    border: 2px solid var(--border);
    background: transparent;
    transition: background 0.15s, border-color 0.15s;
}

.pin-dot.filled {
    background: var(--primary);
    border-color: var(--primary-dark);
}

.pin-error {
    color: var(--danger);
    font-size: 13px;
    min-height: 18px;
    margin-bottom: 8px;
}

@keyframes pin-shake {
    0%, 100% { transform: translateX(0); }
    20%       { transform: translateX(-8px); }
    40%       { transform: translateX(8px); }
    60%       { transform: translateX(-6px); }
    80%       { transform: translateX(6px); }
}

.pin-dots.shake {
    animation: pin-shake 0.5s ease;
}
```

- [ ] **Step 2: Verify visually**

Open the app in a browser/WebView. No visual change expected yet — just confirm no CSS parse errors in DevTools console.

- [ ] **Step 3: Commit**

```bash
git add GruderPOS/wwwroot/css/styles.css
git commit -m "feat: add PIN modal CSS styles and shake animation"
```

---

### Task 2: Add `modal-pin` to `index.html` and load `pin.js`

**Files:**
- Modify: `GruderPOS/wwwroot/index.html` — insert modal before `<!-- Toast Notifications -->` (line 207), add script tag after `history.js` (line 215)

- [ ] **Step 1: Add the PIN modal markup**

In `GruderPOS/wwwroot/index.html`, insert the following block immediately before the `<!-- Toast Notifications -->` comment (line 207):

```html
    <!-- Modal: PIN Security -->
    <div id="modal-pin" class="modal">
        <div class="modal-content">
            <div class="modal-header">
                <h3>Definições</h3>
            </div>
            <div class="modal-body" style="text-align:center;">
                <p style="margin-bottom:16px; color:var(--text-light);">Introduza o PIN de acesso</p>
                <div class="pin-dots" id="pin-dots">
                    <span class="pin-dot"></span>
                    <span class="pin-dot"></span>
                    <span class="pin-dot"></span>
                    <span class="pin-dot"></span>
                </div>
                <p class="pin-error" id="pin-error"></p>
                <div class="numpad">
                    <button class="numpad-btn" onclick="pinAuth._onDigit('1')">1</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('2')">2</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('3')">3</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('4')">4</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('5')">5</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('6')">6</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('7')">7</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('8')">8</button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('9')">9</button>
                    <button class="numpad-btn" style="visibility:hidden;" disabled></button>
                    <button class="numpad-btn" onclick="pinAuth._onDigit('0')">0</button>
                    <button class="numpad-btn numpad-delete" onclick="pinAuth._onBackspace()">⌫</button>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" onclick="pinAuth.cancel()">Cancelar</button>
            </div>
        </div>
    </div>

```

Note: no `✕` close button in the header — the only exit is "Cancelar" in the footer.

- [ ] **Step 2: Add the `pin.js` script tag**

In `GruderPOS/wwwroot/index.html`, add the following line immediately after `<script src="js/history.js"></script>` (line 215):

```html
    <script src="js/pin.js"></script>
```

The final script block should be:

```html
    <script src="js/bridge.js"></script>
    <script src="js/keyboard.js"></script>
    <script src="js/app.js"></script>
    <script src="js/pos.js"></script>
    <script src="js/settings.js"></script>
    <script src="js/history.js"></script>
    <script src="js/pin.js"></script>
```

`pin.js` must load after `app.js` because `pinAuth` reads `app.settings.SettingsPin`.

- [ ] **Step 3: Verify**

Open the app. No visible change expected. Confirm no console errors about missing elements.

- [ ] **Step 4: Commit**

```bash
git add GruderPOS/wwwroot/index.html
git commit -m "feat: add modal-pin markup and load pin.js"
```

---

### Task 3: Create `pin.js` with `pinAuth` object

**Files:**
- Create: `GruderPOS/wwwroot/js/pin.js`

- [ ] **Step 1: Create the file with the full `pinAuth` object**

Create `GruderPOS/wwwroot/js/pin.js` with the following content:

```javascript
// ===== PIN Auth: Settings Access Guard =====
const pinAuth = {
    _digits: '',
    _resolve: null,
    _reject: null,

    request() {
        const storedPin = app.settings.SettingsPin;
        if (!storedPin) {
            return Promise.resolve();
        }
        return new Promise((resolve, reject) => {
            this._resolve = resolve;
            this._reject = reject;
            this._open();
        });
    },

    _open() {
        this._digits = '';
        this._updateIndicator();
        document.getElementById('pin-error').textContent = '';
        document.getElementById('modal-pin').classList.add('active');
    },

    cancel() {
        document.getElementById('modal-pin').classList.remove('active');
        if (this._reject) {
            this._reject(new Error('cancelled'));
            this._resolve = null;
            this._reject = null;
        }
    },

    _onDigit(d) {
        if (this._digits.length >= 4) return;
        this._digits += d;
        this._updateIndicator();
        if (this._digits.length === 4) {
            this._check();
        }
    },

    _onBackspace() {
        if (this._digits.length === 0) return;
        this._digits = this._digits.slice(0, -1);
        this._updateIndicator();
    },

    _updateIndicator() {
        document.querySelectorAll('#pin-dots .pin-dot').forEach((dot, i) => {
            dot.classList.toggle('filled', i < this._digits.length);
        });
    },

    _check() {
        const storedPin = app.settings.SettingsPin;
        if (this._digits === storedPin) {
            document.getElementById('modal-pin').classList.remove('active');
            const resolve = this._resolve;
            this._resolve = null;
            this._reject = null;
            resolve();
        } else {
            const dots = document.getElementById('pin-dots');
            document.getElementById('pin-error').textContent = 'PIN incorreto';
            dots.classList.add('shake');
            setTimeout(() => {
                dots.classList.remove('shake');
                document.getElementById('pin-error').textContent = '';
                this._digits = '';
                this._updateIndicator();
            }, 800);
        }
    }
};
```

- [ ] **Step 2: Verify the object loads without errors**

Open the app in the browser/WebView. Open DevTools console. Type `pinAuth` and confirm the object is defined. No console errors expected.

- [ ] **Step 3: Manually test the modal in isolation (no PIN set yet)**

In DevTools console, temporarily set a PIN:

```javascript
app.settings.SettingsPin = '1234';
pinAuth.request();
```

Expected: PIN modal opens. Enter `1234` using the numpad → modal closes, promise resolves. Try a wrong PIN → dots shake, "PIN incorreto" shown, digits clear after ~800ms.

- [ ] **Step 4: Test cancel**

```javascript
app.settings.SettingsPin = '1234';
pinAuth.request().catch(e => console.log('cancelled:', e.message));
```

Expected: modal opens. Click "Cancelar" → modal closes, console logs `cancelled: cancelled`.

- [ ] **Step 5: Reset test state**

```javascript
app.settings.SettingsPin = undefined;
```

- [ ] **Step 6: Commit**

```bash
git add GruderPOS/wwwroot/js/pin.js
git commit -m "feat: add pinAuth object for settings PIN gate"
```

---

### Task 4: Intercept `navigate('settings')` in `app.js`

**Files:**
- Modify: `GruderPOS/wwwroot/js/app.js` — `navigate()` method (lines 44–59)

- [ ] **Step 1: Replace `navigate()` with a guarded version**

In `GruderPOS/wwwroot/js/app.js`, replace the entire `navigate(page)` method (lines 44–59) with:

```javascript
    navigate(page) {
        if (page === 'settings') {
            pinAuth.request().then(() => this._doNavigate('settings')).catch(() => {});
            return;
        }
        this._doNavigate(page);
    },

    _doNavigate(page) {
        document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
        document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));

        document.getElementById(`page-${page}`).classList.add('active');
        document.querySelector(`.nav-item[data-page="${page}"]`).classList.add('active');

        this.currentPage = page;

        if (page === 'history') history.init();
        if (page === 'settings') settings.init();
        if (page === 'pos') pos.refreshProducts();
    },
```

- [ ] **Step 2: Verify navigation still works without PIN**

Open the app. Confirm POS, Histórico, and Definições navigation all work normally (no PIN is configured yet so settings opens freely). No console errors.

- [ ] **Step 3: Verify PIN gate activates when a PIN is set**

In DevTools console:

```javascript
app.settings.SettingsPin = '4321';
```

Click "Definições" in the sidebar. Expected: PIN modal opens. Enter `4321` → modal closes, Settings page opens. Click another nav item, then click "Definições" again → PIN modal appears again.

- [ ] **Step 4: Verify cancel aborts navigation**

With `app.settings.SettingsPin = '4321'` still set, click "Definições". When modal opens, click "Cancelar". Expected: modal closes, current page does not change to Settings.

- [ ] **Step 5: Reset and commit**

```javascript
app.settings.SettingsPin = undefined;
```

```bash
git add GruderPOS/wwwroot/js/app.js
git commit -m "feat: intercept settings navigation with PIN gate"
```

---

### Task 5: Add PIN management to General Settings

**Files:**
- Modify: `GruderPOS/wwwroot/js/settings.js` — `renderGeneral()` method and new PIN helper methods

- [ ] **Step 1: Extend `renderGeneral()` to include a Security section**

In `GruderPOS/wwwroot/js/settings.js`, replace the `renderGeneral()` method (lines 776–793) with:

```javascript
    async renderGeneral(container) {
        let appSettings = {};
        try {
            appSettings = await bridge.send('getSettings');
        } catch (e) {}

        const hasPin = !!(appSettings.SettingsPin);

        const pinSection = hasPin ? `
            <div class="form-group">
                <label>PIN Atual</label>
                <input type="password" id="pin-current" class="form-input" maxlength="4" placeholder="••••" inputmode="numeric">
            </div>
            <div class="form-group">
                <label>Novo PIN</label>
                <input type="password" id="pin-new" class="form-input" maxlength="4" placeholder="••••" inputmode="numeric">
            </div>
            <div class="form-group">
                <label>Confirmar Novo PIN</label>
                <input type="password" id="pin-confirm" class="form-input" maxlength="4" placeholder="••••" inputmode="numeric">
            </div>
            <div style="display:flex; gap:8px; margin-top:8px;">
                <button class="btn btn-primary" onclick="settings.savePin(this)">Alterar PIN</button>
                <button class="btn btn-danger btn-small" onclick="settings.removePin(this)">Remover PIN</button>
            </div>` : `
            <p style="font-size:13px; color:var(--text-muted); margin-bottom:12px;">
                Sem PIN configurado. Definir um PIN protege o acesso às configurações.
            </p>
            <div class="form-group">
                <label>Novo PIN (4 dígitos)</label>
                <input type="password" id="pin-new" class="form-input" maxlength="4" placeholder="••••" inputmode="numeric">
            </div>
            <div class="form-group">
                <label>Confirmar PIN</label>
                <input type="password" id="pin-confirm" class="form-input" maxlength="4" placeholder="••••" inputmode="numeric">
            </div>
            <div style="margin-top:8px;">
                <button class="btn btn-primary" onclick="settings.savePin(this)">Definir PIN</button>
            </div>`;

        container.innerHTML = `
            <div class="settings-form">
                <h3 style="font-size: 16px; margin-bottom: 20px;">Definições Gerais</h3>
                <div class="form-group">
                    <label>Nome do Evento / Festa</label>
                    <div class="input-with-keyboard"><input type="text" id="setting-event" class="form-input" value="${appSettings.EventName || 'Festa GRUDER 2026'}" placeholder="Ex: Festa de Verão 2026"><button class="btn-keyboard" data-target="setting-event" data-label="Nome do Evento">⌨</button></div>
                </div>
                <div style="margin-top: 20px;">
                    <button class="btn btn-primary" onclick="settings.saveGeneralSettings(this)">Guardar</button>
                </div>
            </div>

            <div class="settings-form" style="margin-top:16px;">
                <h3 style="font-size:16px; margin-bottom:16px;">Segurança</h3>
                ${pinSection}
            </div>`;
    },
```

- [ ] **Step 2: Add `savePin()` method to the `settings` object**

In `GruderPOS/wwwroot/js/settings.js`, add the following two methods immediately before the closing `}` of the `settings` object (before line 811 `};`):

```javascript
    async savePin(btn) {
        const currentInput = document.getElementById('pin-current');
        const newPin = (document.getElementById('pin-new')?.value || '').trim();
        const confirm = (document.getElementById('pin-confirm')?.value || '').trim();

        if (currentInput) {
            const currentPin = currentInput.value.trim();
            if (currentPin !== app.settings.SettingsPin) {
                showToast('PIN atual incorreto', 'error');
                return;
            }
        }

        if (!/^\d{4}$/.test(newPin)) {
            showToast('O PIN deve ter exatamente 4 dígitos numéricos', 'warning');
            return;
        }

        if (newPin !== confirm) {
            showToast('Os PINs não coincidem', 'warning');
            return;
        }

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveSettings', { data: { SettingsPin: newPin } });
            app.settings.SettingsPin = newPin;
            showToast('PIN definido com sucesso!', 'success');
            await this.renderGeneral(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async removePin(btn) {
        if (!confirm('Tem a certeza que pretende remover o PIN de acesso?')) return;
        setButtonLoading(btn, true);
        try {
            await bridge.send('saveSettings', { data: { SettingsPin: '' } });
            app.settings.SettingsPin = '';
            showToast('PIN removido', 'success');
            await this.renderGeneral(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    }
```

- [ ] **Step 3: Verify General settings renders the Security section**

Open the app, navigate to Definições → Geral. Expected: "Segurança" section visible below "Definições Gerais" with the "no PIN set" state (fields for Novo PIN + Confirmar PIN + "Definir PIN" button).

- [ ] **Step 4: Test setting a PIN**

Enter `1234` in both PIN fields, click "Definir PIN". Expected:
- Toast "PIN definido com sucesso!" appears
- Section re-renders showing the "has PIN" state (PIN atual + Novo PIN + Confirmar + Alterar + Remover)
- `app.settings.SettingsPin` equals `'1234'` in console

- [ ] **Step 5: Test PIN gate now active**

Navigate away (e.g. Caixa), then click Definições. Expected: PIN modal appears. Enter `1234` → opens. Enter wrong PIN → shake + error.

- [ ] **Step 6: Test changing PIN**

Go to Definições → Geral → Segurança. Enter PIN atual `1234`, Novo PIN `5678`, Confirmar `5678`. Click "Alterar PIN". Expected: toast success, `app.settings.SettingsPin` is now `'5678'`. Confirm new PIN works at the gate.

- [ ] **Step 7: Test removing PIN**

Go to Definições → Geral → Segurança. Click "Remover PIN". Confirm dialog → Expected: toast "PIN removido", section shows "no PIN" state. Navigate away and back to Definições → settings opens without modal.

- [ ] **Step 8: Test validation errors**

- Submit with only 3 digits → toast "O PIN deve ter exatamente 4 dígitos numéricos"
- Submit with non-matching confirm → toast "Os PINs não coincidem"
- (When changing) Enter wrong current PIN → toast "PIN atual incorreto"

- [ ] **Step 9: Commit**

```bash
git add GruderPOS/wwwroot/js/settings.js
git commit -m "feat: add PIN management section in General settings"
```

---

### Task 6: End-to-end smoke test

- [ ] **Step 1: Full flow test**

1. Launch app fresh (no PIN in settings)
2. Click Definições → opens without PIN modal ✓
3. Go to Geral → set PIN `9876` → toast success ✓
4. Navigate to Caixa → click Definições → PIN modal appears ✓
5. Enter wrong PIN → shake + "PIN incorreto" ✓
6. Enter `9876` → Settings opens ✓
7. Click Cancelar on PIN modal → stays on current page ✓
8. Go to Definições again → PIN required again ✓
9. In Geral, change PIN to `1111` → works ✓
10. Verify `9876` no longer works, `1111` works ✓
11. Remove PIN → Definições opens freely ✓

- [ ] **Step 2: Restart app and verify PIN persists**

Close and reopen the app. Set PIN was `1111` before removal, so no PIN should be set. Confirm Definições opens freely (PIN was removed). If you had set a new PIN before closing, confirm it is still required after restart (persisted via backend settings).
