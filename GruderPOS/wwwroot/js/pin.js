// ===== PIN Auth: Settings Access Guard =====
const pinAuth = {
    _digits: '',
    _resolve: null,
    _reject: null,
    _checking: false,
    _shakeTimer: null,

    request() {
        const storedPin = app.settings.SettingsPin;
        if (!storedPin) {
            return Promise.resolve();
        }
        if (this._resolve) {
            const oldReject = this._reject;
            return new Promise((resolve, reject) => {
                this._resolve = resolve;
                this._reject = reject;
                oldReject(new Error('cancelled'));
            });
        }
        return new Promise((resolve, reject) => {
            this._resolve = resolve;
            this._reject = reject;
            this._open();
        });
    },

    _open() {
        this._digits = '';
        this._checking = false;
        if (this._shakeTimer) {
            clearTimeout(this._shakeTimer);
            this._shakeTimer = null;
        }
        this._updateIndicator();
        document.getElementById('pin-error').textContent = '';
        document.getElementById('modal-pin').classList.add('active');
    },

    cancel() {
        if (this._shakeTimer) {
            clearTimeout(this._shakeTimer);
            this._shakeTimer = null;
        }
        this._checking = false;
        this._digits = '';
        this._updateIndicator();
        document.getElementById('pin-error').textContent = '';
        document.getElementById('modal-pin').classList.remove('active');
        if (this._reject) {
            this._reject(new Error('cancelled'));
            this._resolve = null;
            this._reject = null;
        }
    },

    _onDigit(d) {
        if (this._checking || this._digits.length >= 4) return;
        this._digits += d;
        this._updateIndicator();
        if (this._digits.length === 4) {
            this._check();
        }
    },

    _onBackspace() {
        if (this._checking || this._digits.length === 0) return;
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
            this._checking = true;
            const dots = document.getElementById('pin-dots');
            document.getElementById('pin-error').textContent = 'PIN incorreto';
            dots.classList.add('shake');
            this._shakeTimer = setTimeout(() => {
                this._shakeTimer = null;
                this._checking = false;
                dots.classList.remove('shake');
                document.getElementById('pin-error').textContent = '';
                this._digits = '';
                this._updateIndicator();
            }, 800);
        }
    }
};

// ===== PIN Input Numpad: fills PIN form fields =====
const pinNumpad = {
    _targetId: null,
    _digits: '',

    open(targetId, label) {
        this._targetId = targetId;
        this._digits = '';
        document.getElementById('pin-input-label').textContent = label || 'PIN';
        this._updateIndicator();
        document.getElementById('modal-pin-input').classList.add('active');
    },

    _onDigit(d) {
        if (this._digits.length >= 4) return;
        this._digits += d;
        this._updateIndicator();
        if (this._digits.length === 4) {
            this._confirm();
        }
    },

    _onBackspace() {
        if (this._digits.length === 0) return;
        this._digits = this._digits.slice(0, -1);
        this._updateIndicator();
    },

    _updateIndicator() {
        document.querySelectorAll('#pin-input-dots .pin-dot').forEach((dot, i) => {
            dot.classList.toggle('filled', i < this._digits.length);
        });
    },

    _confirm() {
        const el = document.getElementById(this._targetId);
        if (el) {
            el.value = this._digits;
            el.dispatchEvent(new Event('input'));
        }
        this._close();
    },

    _cancel() {
        this._close();
    },

    _close() {
        this._targetId = null;
        this._digits = '';
        document.getElementById('modal-pin-input').classList.remove('active');
    },

    init() {
        document.addEventListener('click', e => {
            const btn = e.target.closest('.btn-numpad');
            if (!btn) return;
            this.open(btn.dataset.target, btn.dataset.label || 'PIN');
        });
    }
};
