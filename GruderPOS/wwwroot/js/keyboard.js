// ===== Virtual Keyboard =====
const virtualKeyboard = {
    _target: null,
    _buffer: '',
    _shift: false,
    _symbols: false,
    _initialized: false,

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
            [';',':','\'','"','<','>','^','ABC','DEL'],
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
        if (!kb) return;
        kb.innerHTML = this._rows[layer].map(row =>
            `<div class="vk-row">${row.map(k => {
                const safeKey = k.replace(/&/g,'&amp;').replace(/"/g,'&quot;').replace(/'/g,'&#39;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
                return `<button class="${this._keyClass(k)}" data-key="${safeKey}">${this._keyLabel(k)}</button>`;
            }).join('')}</div>`
        ).join('');
    },

    _updateDisplay() {
        const el = document.getElementById('vk-display');
        if (el) el.textContent = this._buffer;
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
        if (this._initialized) return;
        this._initialized = true;
        document.addEventListener('click', e => {
            const btn = e.target.closest('.btn-keyboard');
            if (!btn) return;
            const el = document.getElementById(btn.dataset.target);
            if (el) this.open(el, btn.dataset.label || '');
        });
        document.getElementById('modal-keyboard').addEventListener('click', e => {
            const btn = e.target.closest('button[data-key]');
            if (btn) this._key(btn.dataset.key);
        });
    }
};
