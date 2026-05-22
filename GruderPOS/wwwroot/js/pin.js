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
