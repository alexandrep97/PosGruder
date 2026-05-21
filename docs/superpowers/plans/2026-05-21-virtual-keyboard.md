# Virtual Keyboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a touch-friendly virtual keyboard modal to every text input field in GruderPOS, triggered by an explicit ⌨️ button next to each field.

**Architecture:** A singleton `virtualKeyboard` module (`keyboard.js`) owns a shared modal. Any `.btn-keyboard` button click opens the modal pre-filled with the target field's value; OK writes back and fires `input` event; Cancel discards. Numeric fields (numpad) and date fields are left unchanged.

**Tech Stack:** Vanilla JavaScript (ES6), custom CSS with CSS variables, no external libraries.

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `wwwroot/js/keyboard.js` | Singleton `virtualKeyboard` module |
| Modify | `wwwroot/css/styles.css` | `.input-with-keyboard`, `.btn-keyboard`, `.vk-*` classes |
| Modify | `wwwroot/index.html` | `#modal-keyboard` HTML, `keyboard.js` script tag, ⌨️ on `#generic-description` |
| Modify | `wwwroot/js/app.js` | `virtualKeyboard.init()` call in `app.init()`; ⌨️ on `#close-notes` |
| Modify | `wwwroot/js/settings.js` | ⌨️ buttons on all 11 settings text fields |

---

## Task 1: CSS — Virtual Keyboard Styles

**Files:**
- Modify: `GruderPOS/wwwroot/css/styles.css` (append at end of file)

- [ ] **Step 1: Append virtual keyboard CSS to `styles.css`**

Add the following block at the very end of `styles.css`:

```css
/* ===== Virtual Keyboard ===== */
.input-with-keyboard {
    display: flex;
    gap: 8px;
    align-items: center;
    width: 100%;
}

.input-with-keyboard .form-input {
    flex: 1;
    min-width: 0;
    width: auto;
}

.btn-keyboard {
    flex-shrink: 0;
    min-width: 44px;
    min-height: 44px;
    padding: 0;
    border: 2px solid var(--border);
    border-radius: var(--radius-sm);
    background: var(--bg-dark);
    color: var(--primary);
    font-size: 18px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: var(--transition);
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
}

.btn-keyboard:active {
    transform: scale(0.95);
    background: var(--primary);
    color: var(--secondary);
}

#modal-keyboard .modal-content {
    max-width: 520px;
}

.vk-display {
    width: 100%;
    min-height: 48px;
    padding: 12px 16px;
    border: 2px solid var(--primary);
    border-radius: var(--radius-sm);
    font-size: 18px;
    font-family: var(--font);
    background: var(--bg);
    color: var(--text);
    word-break: break-all;
    margin-bottom: 12px;
}

.vk-display::after {
    content: '|';
    animation: vk-cursor 1s step-end infinite;
    color: var(--primary);
    font-weight: 100;
    margin-left: 1px;
}

@keyframes vk-cursor {
    0%, 100% { opacity: 1; }
    50% { opacity: 0; }
}

.vk-keyboard {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.vk-row {
    display: flex;
    gap: 4px;
    justify-content: center;
}

.vk-key {
    flex: 1;
    min-height: 44px;
    padding: 4px 2px;
    border: 1.5px solid var(--border);
    border-radius: var(--radius-xs);
    background: var(--card-bg);
    color: var(--text);
    font-size: 13px;
    font-family: var(--font);
    font-weight: 500;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: background 0.1s, transform 0.1s;
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    user-select: none;
    -webkit-user-select: none;
}

.vk-key:active {
    transform: scale(0.92);
    background: var(--bg-dark);
}

.vk-key-wide {
    flex: 1.5;
}

.vk-key-space {
    flex: 3;
    color: var(--text-muted);
    font-size: 12px;
}

.vk-key-action {
    background: var(--bg-dark);
    color: var(--text-light);
    font-size: 11px;
}

.vk-key--active {
    background: var(--primary) !important;
    color: var(--secondary) !important;
    border-color: var(--primary-dark);
}
```

- [ ] **Step 2: Commit**

```bash
git add GruderPOS/wwwroot/css/styles.css
git commit -m "style: add virtual keyboard CSS classes"
```

---

## Task 2: Create `keyboard.js`

**Files:**
- Create: `GruderPOS/wwwroot/js/keyboard.js`

- [ ] **Step 1: Create `keyboard.js` with the singleton module**

```javascript
// ===== Virtual Keyboard =====
const virtualKeyboard = {
    _target: null,
    _buffer: '',
    _shift: false,
    _symbols: false,

    _rows: {
        lower: [
            ['1','2','3','4','5','6','7','8','9','0'],
            ['q','w','e','r','t','y','u','i','o','p'],
            ['a','s','d','f','g','h','j','k','l'],
            ['SHIFT','z','x','c','v','b','n','m','DEL'],
            ['SYM','SPACE','.', ',']
        ],
        upper: [
            ['1','2','3','4','5','6','7','8','9','0'],
            ['Q','W','E','R','T','Y','U','I','O','P'],
            ['A','S','D','F','G','H','J','K','L'],
            ['SHIFT','Z','X','C','V','B','N','M','DEL'],
            ['SYM','SPACE','.', ',']
        ],
        symbols: [
            ['!','@','#','$','%','&','*','(',')','€'],
            ['_','-','=','+','[',']','/','?','|'],
            [';',':','\'','"','<','>','!','ABC','DEL'],
            ['ABC','SPACE','.', ',']
        ]
    },

    _keyLabel(k) {
        if (k === 'SHIFT') return '⇧';
        if (k === 'DEL')   return '⌫';
        if (k === 'SPACE') return 'espaço';
        if (k === 'SYM')   return '!@#';
        if (k === 'ABC')   return 'abc';
        return k;
    },

    _keyClass(k) {
        let cls = 'vk-key';
        if (['SHIFT','DEL','SYM','ABC'].includes(k)) cls += ' vk-key-action';
        if (k === 'SHIFT' || k === 'DEL') cls += ' vk-key-wide';
        if (k === 'SPACE') cls += ' vk-key-space';
        if (k === 'SHIFT' && this._shift) cls += ' vk-key--active';
        return cls;
    },

    _render() {
        const layer = this._symbols ? 'symbols' : (this._shift ? 'upper' : 'lower');
        const kb = document.getElementById('vk-keyboard');
        kb.innerHTML = this._rows[layer].map(row =>
            `<div class="vk-row">${row.map(k =>
                `<button class="${this._keyClass(k)}" onclick="virtualKeyboard._key(${JSON.stringify(k)})">${this._keyLabel(k)}</button>`
            ).join('')}</div>`
        ).join('');
    },

    _updateDisplay() {
        document.getElementById('vk-display').textContent = this._buffer;
    },

    _key(k) {
        switch (k) {
            case 'DEL':
                this._buffer = this._buffer.slice(0, -1);
                break;
            case 'SPACE':
                this._buffer += ' ';
                break;
            case 'SHIFT':
                this._shift = !this._shift;
                this._render();
                return;
            case 'SYM':
                this._symbols = true;
                this._render();
                return;
            case 'ABC':
                this._symbols = false;
                this._shift = false;
                this._render();
                return;
            default:
                this._buffer += k;
                if (this._shift && !this._symbols) {
                    this._shift = false;
                    this._render();
                }
        }
        this._updateDisplay();
    },

    open(el, label) {
        this._target = el;
        this._buffer = el.value;
        this._shift = false;
        this._symbols = false;
        document.getElementById('vk-label').textContent = label;
        this._render();
        this._updateDisplay();
        document.getElementById('modal-keyboard').classList.add('active');
    },

    _confirm() {
        if (this._target) {
            this._target.value = this._buffer;
            this._target.dispatchEvent(new Event('input'));
        }
        this._close();
    },

    _cancel() {
        this._close();
    },

    _close() {
        this._target = null;
        this._buffer = '';
        document.getElementById('modal-keyboard').classList.remove('active');
    },

    init() {
        document.addEventListener('click', e => {
            const btn = e.target.closest('.btn-keyboard');
            if (!btn) return;
            const el = document.getElementById(btn.dataset.target);
            if (el) this.open(el, btn.dataset.label || '');
        });
    }
};
```

- [ ] **Step 2: Commit**

```bash
git add GruderPOS/wwwroot/js/keyboard.js
git commit -m "feat: add virtualKeyboard singleton module"
```

---

## Task 3: `index.html` — Modal HTML, script tag, `#generic-description` button

**Files:**
- Modify: `GruderPOS/wwwroot/index.html`

- [ ] **Step 1: Add `#modal-keyboard` HTML before the toast container**

Find this line in `index.html` (line 187):
```html
    <!-- Toast Notifications -->
```

Insert before it:
```html
    <!-- Modal: Virtual Keyboard -->
    <div id="modal-keyboard" class="modal">
        <div class="modal-content">
            <div class="modal-header">
                <h3 id="vk-label">Texto</h3>
                <button class="modal-close" onclick="virtualKeyboard._cancel()">✕</button>
            </div>
            <div class="modal-body">
                <div id="vk-display" class="vk-display"></div>
                <div id="vk-keyboard" class="vk-keyboard"></div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" onclick="virtualKeyboard._cancel()">Cancelar</button>
                <button class="btn btn-primary" onclick="virtualKeyboard._confirm()">OK — Confirmar</button>
            </div>
        </div>
    </div>

```

- [ ] **Step 2: Add `keyboard.js` script tag after `bridge.js`**

Find (line 189):
```html
    <script src="js/bridge.js"></script>
    <script src="js/app.js"></script>
```

Replace with:
```html
    <script src="js/bridge.js"></script>
    <script src="js/keyboard.js"></script>
    <script src="js/app.js"></script>
```

- [ ] **Step 3: Wrap `#generic-description` in `.input-with-keyboard`**

Find in `index.html` (around line 133):
```html
                <div class="form-group">
                    <label>Descrição</label>
                    <input type="text" id="generic-description" placeholder="Ex: Rifa, Entrada..." class="form-input">
                </div>
```

Replace with:
```html
                <div class="form-group">
                    <label>Descrição</label>
                    <div class="input-with-keyboard">
                        <input type="text" id="generic-description" placeholder="Ex: Rifa, Entrada..." class="form-input">
                        <button class="btn-keyboard" data-target="generic-description" data-label="Descrição">⌨</button>
                    </div>
                </div>
```

- [ ] **Step 4: Commit**

```bash
git add GruderPOS/wwwroot/index.html
git commit -m "feat: add keyboard modal HTML and button to generic description field"
```

---

## Task 4: `app.js` — Init call + `#close-notes` button

**Files:**
- Modify: `GruderPOS/wwwroot/js/app.js`

- [ ] **Step 1: Add `virtualKeyboard.init()` to `app.init()`**

In `app.js`, find the end of the `async init()` method. The method ends with the session indicator `onclick` assignment. Add the init call after it:

Find:
```javascript
        // Session indicator click handler
        document.getElementById('session-indicator').onclick = () => {
            if (this.currentSession && this.currentSession.id) {
                this.showCloseSessionModal();
            } else {
                this.showOpenSessionModal();
            }
        };
    },
```

Replace with:
```javascript
        // Session indicator click handler
        document.getElementById('session-indicator').onclick = () => {
            if (this.currentSession && this.currentSession.id) {
                this.showCloseSessionModal();
            } else {
                this.showOpenSessionModal();
            }
        };

        virtualKeyboard.init();
    },
```

- [ ] **Step 2: Add ⌨️ button to `#close-notes` textarea in `showCloseSessionModal()`**

In `app.js`, find inside `showCloseSessionModal()` (around line 124):
```javascript
            <div class="form-group">
                <label>Notas (opcional)</label>
                <textarea id="close-notes" class="form-input" rows="3" placeholder="Observações sobre o fecho de caixa..."></textarea>
            </div>
```

Replace with:
```javascript
            <div class="form-group">
                <label>Notas (opcional)</label>
                <div class="input-with-keyboard">
                    <textarea id="close-notes" class="form-input" rows="3" placeholder="Observações sobre o fecho de caixa..."></textarea>
                    <button class="btn-keyboard" data-target="close-notes" data-label="Notas">⌨</button>
                </div>
            </div>
```

- [ ] **Step 3: Commit**

```bash
git add GruderPOS/wwwroot/js/app.js
git commit -m "feat: wire virtualKeyboard init and add button to close-notes textarea"
```

---

## Task 5: `settings.js` — ⌨️ buttons on all settings text fields

**Files:**
- Modify: `GruderPOS/wwwroot/js/settings.js`

- [ ] **Step 1: Wrap `#cat-name` in `showCategoryForm()`**

Find (around line 115):
```javascript
                <input type="text" id="cat-name" class="form-input" value="${cat ? cat.name : ''}" placeholder="Ex: Bebidas, Comida...">
```

Replace with:
```javascript
                <div class="input-with-keyboard"><input type="text" id="cat-name" class="form-input" value="${cat ? cat.name : ''}" placeholder="Ex: Bebidas, Comida..."><button class="btn-keyboard" data-target="cat-name" data-label="Nome da Categoria">⌨</button></div>
```

- [ ] **Step 2: Wrap `#prod-name` in `showProductForm()`**

Find (around line 263):
```javascript
                <input type="text" id="prod-name" class="form-input" value="${prod ? prod.name : ''}" placeholder="Ex: Bifana, Cerveja...">
```

Replace with:
```javascript
                <div class="input-with-keyboard"><input type="text" id="prod-name" class="form-input" value="${prod ? prod.name : ''}" placeholder="Ex: Bifana, Cerveja..."><button class="btn-keyboard" data-target="prod-name" data-label="Nome do Produto">⌨</button></div>
```

- [ ] **Step 3: Wrap receipt header fields in `renderReceiptLayout()`**

Find (around line 414):
```javascript
                    <div class="form-group"><label>Linha 1 (Título principal)</label><input type="text" id="receipt-h1" class="form-input" value="${h1}" placeholder="Ex: GRUDER"></div>
                    <div class="form-group"><label>Linha 2</label><input type="text" id="receipt-h2" class="form-input" value="${h2}" placeholder="Ex: GRUPO DESPORTIVO DA"></div>
                    <div class="form-group"><label>Linha 3</label><input type="text" id="receipt-h3" class="form-input" value="${h3}" placeholder="Ex: RIBEIRA DO FARRIO"></div>
                    <div class="form-group"><label>Linha 4</label><input type="text" id="receipt-h4" class="form-input" value="${h4}" placeholder="Ex: Fundado em 1977"></div>
```

Replace with:
```javascript
                    <div class="form-group"><label>Linha 1 (Título principal)</label><div class="input-with-keyboard"><input type="text" id="receipt-h1" class="form-input" value="${h1}" placeholder="Ex: GRUDER"><button class="btn-keyboard" data-target="receipt-h1" data-label="Cabeçalho Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha 2</label><div class="input-with-keyboard"><input type="text" id="receipt-h2" class="form-input" value="${h2}" placeholder="Ex: GRUPO DESPORTIVO DA"><button class="btn-keyboard" data-target="receipt-h2" data-label="Cabeçalho Linha 2">⌨</button></div></div>
                    <div class="form-group"><label>Linha 3</label><div class="input-with-keyboard"><input type="text" id="receipt-h3" class="form-input" value="${h3}" placeholder="Ex: RIBEIRA DO FARRIO"><button class="btn-keyboard" data-target="receipt-h3" data-label="Cabeçalho Linha 3">⌨</button></div></div>
                    <div class="form-group"><label>Linha 4</label><div class="input-with-keyboard"><input type="text" id="receipt-h4" class="form-input" value="${h4}" placeholder="Ex: Fundado em 1977"><button class="btn-keyboard" data-target="receipt-h4" data-label="Cabeçalho Linha 4">⌨</button></div></div>
```

- [ ] **Step 4: Wrap receipt body fields in `renderReceiptLayout()`**

Find (around line 434):
```javascript
                    <div class="form-group"><label>Título do Corpo (negrito)</label><input type="text" id="receipt-bt" class="form-input" value="${bt}" placeholder="Ex: Festa GRUDER 2026"></div>
                    <div class="form-group"><label>Linha adicional 1</label><input type="text" id="receipt-b1" class="form-input" value="${b1}" placeholder="Ex: Bar Principal"></div>
                    <div class="form-group"><label>Linha adicional 2</label><input type="text" id="receipt-b2" class="form-input" value="${b2}" placeholder=""></div>
```

Replace with:
```javascript
                    <div class="form-group"><label>Título do Corpo (negrito)</label><div class="input-with-keyboard"><input type="text" id="receipt-bt" class="form-input" value="${bt}" placeholder="Ex: Festa GRUDER 2026"><button class="btn-keyboard" data-target="receipt-bt" data-label="Título do Corpo">⌨</button></div></div>
                    <div class="form-group"><label>Linha adicional 1</label><div class="input-with-keyboard"><input type="text" id="receipt-b1" class="form-input" value="${b1}" placeholder="Ex: Bar Principal"><button class="btn-keyboard" data-target="receipt-b1" data-label="Corpo Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha adicional 2</label><div class="input-with-keyboard"><input type="text" id="receipt-b2" class="form-input" value="${b2}" placeholder=""><button class="btn-keyboard" data-target="receipt-b2" data-label="Corpo Linha 2">⌨</button></div></div>
```

- [ ] **Step 5: Wrap receipt footer fields in `renderReceiptLayout()`**

Find (around line 461):
```javascript
                    <div class="form-group"><label>Linha 1</label><input type="text" id="receipt-f1" class="form-input" value="${f1}" placeholder="Ex: Obrigado pela preferência!"></div>
                    <div class="form-group"><label>Linha 2 (Destaque)</label><input type="text" id="receipt-f2" class="form-input" value="${f2}" placeholder="Ex: GRUDER - 1977"></div>
```

Replace with:
```javascript
                    <div class="form-group"><label>Linha 1</label><div class="input-with-keyboard"><input type="text" id="receipt-f1" class="form-input" value="${f1}" placeholder="Ex: Obrigado pela preferência!"><button class="btn-keyboard" data-target="receipt-f1" data-label="Rodapé Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha 2 (Destaque)</label><div class="input-with-keyboard"><input type="text" id="receipt-f2" class="form-input" value="${f2}" placeholder="Ex: GRUDER - 1977"><button class="btn-keyboard" data-target="receipt-f2" data-label="Rodapé Linha 2">⌨</button></div></div>
```

- [ ] **Step 6: Wrap `#setting-event` in `renderGeneral()`**

Find (around line 765):
```javascript
                    <input type="text" id="setting-event" class="form-input" value="${appSettings.EventName || 'Festa GRUDER 2026'}" placeholder="Ex: Festa de Verão 2026">
```

Replace with:
```javascript
                    <div class="input-with-keyboard"><input type="text" id="setting-event" class="form-input" value="${appSettings.EventName || 'Festa GRUDER 2026'}" placeholder="Ex: Festa de Verão 2026"><button class="btn-keyboard" data-target="setting-event" data-label="Nome do Evento">⌨</button></div>
```

- [ ] **Step 7: Commit**

```bash
git add GruderPOS/wwwroot/js/settings.js
git commit -m "feat: add virtual keyboard buttons to all settings text fields"
```

---

## Task 6: Verification

- [ ] **Step 1: Build and run the application**

Open the solution in Visual Studio or run:
```powershell
cd "GruderPOS"
dotnet run
```

- [ ] **Step 2: Verify — POS Page (Artigo Genérico)**

1. Click the "Artigo Genérico" card in the products grid
2. The generic product modal opens
3. A ⌨️ button appears to the right of the "Descrição" field
4. Tap ⌨️ — the keyboard modal opens, title reads "Descrição", display is empty
5. Type "Rifa" using the keyboard — display updates live
6. Tap "OK — Confirmar" — modal closes, "Descrição" field now contains "Rifa"
7. Open modal again, tap ⌨️, type "Test", tap "Cancelar" — field still reads "Rifa"

- [ ] **Step 3: Verify — Shift and symbols**

1. Open any keyboard modal
2. Tap ⇧ — shift button turns yellow, next letter typed is uppercase, shift releases automatically
3. Tap ⇧ twice — shift activates, second tap deactivates
4. Tap "!@#" — symbols layer shows; tap "abc" — returns to lowercase layer

- [ ] **Step 4: Verify — Session Close modal**

1. Open a cash session (click session indicator → Abrir Caixa)
2. Click session indicator again → Fechar Caixa modal opens
3. ⌨️ button appears next to the "Notas" textarea
4. Tap ⌨️ — keyboard modal opens with label "Notas"
5. Type a note, tap OK — textarea contains the typed text

- [ ] **Step 5: Verify — Settings / Categorias**

1. Navigate to Definições → Categorias
2. Click "+ Nova Categoria"
3. ⌨️ button appears next to "Nome da Categoria" field
4. Tap ⌨️, type "Drinks", tap OK — field shows "Drinks"

- [ ] **Step 6: Verify — Settings / Layout Talão**

1. Navigate to Definições → Layout Talão
2. ⌨️ buttons appear next to all 9 receipt text fields (H1–H4, BT, B1–B2, F1–F2)
3. Tap ⌨️ on "Linha 1 (Título principal)", type "GRUDER TEST", tap OK
4. The receipt preview at the bottom updates immediately (because `input` event is dispatched)

- [ ] **Step 7: Verify — Settings / Geral**

1. Navigate to Definições → Geral
2. ⌨️ button appears next to "Nome do Evento / Festa"
3. Tap ⌨️, change the event name, tap OK, tap Guardar — setting is saved

- [ ] **Step 8: Final commit if any fixes were made during verification**

```bash
git add -p
git commit -m "fix: virtual keyboard post-verification corrections"
```
