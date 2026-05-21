// ===== App: Main Application Controller =====
const app = {
    currentPage: 'pos',
    currentSession: null,
    settings: {},

    async init() {
        await this.loadSettings();
        await this.checkSession();
        await pos.init();
        this.updateSessionUI();

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

    async loadSettings() {
        try {
            this.settings = await bridge.send('getSettings');
        } catch (e) {
            console.error('Failed to load settings:', e);
            this.settings = {};
        }
    },

    async checkSession() {
        try {
            const session = await bridge.send('getCurrentSession');
            this.currentSession = session && session.id ? session : null;
        } catch (e) {
            console.error('Failed to check session:', e);
            this.currentSession = null;
        }
    },

    navigate(page) {
        // Hide all pages
        document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
        document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));

        // Show selected page
        document.getElementById(`page-${page}`).classList.add('active');
        document.querySelector(`.nav-item[data-page="${page}"]`).classList.add('active');

        this.currentPage = page;

        // Initialize page if needed
        if (page === 'history') history.init();
        if (page === 'settings') settings.init();
        if (page === 'pos') pos.refreshProducts();
    },

    updateSessionUI() {
        const indicator = document.getElementById('session-indicator');
        const sessionText = document.getElementById('session-text');

        if (this.currentSession && this.currentSession.id) {
            indicator.className = 'session-open';
            sessionText.textContent = 'Caixa\nAberta';
        } else {
            indicator.className = 'session-closed';
            sessionText.textContent = 'Caixa\nFechada';
        }
    },

    showOpenSessionModal() {
        const modal = document.getElementById('modal-session');
        const title = document.getElementById('session-modal-title');
        const body = document.getElementById('session-modal-body');
        const footer = document.getElementById('session-modal-footer');

        title.textContent = 'Abrir Caixa';
        body.innerHTML = `
            <div class="form-group">
                <label>Fundo de Caixa (€)</label>
                <input type="number" id="opening-balance" class="form-input form-input-large" value="50.00" step="0.01" min="0">
            </div>
            <p class="text-muted" style="font-size: 13px;">Introduza o valor inicial disponível na caixa.</p>
        `;
        footer.innerHTML = `
            <button class="btn btn-secondary" onclick="app.closeSessionModal()">Cancelar</button>
            <button class="btn btn-success" onclick="app.openSession()">Abrir Caixa</button>
        `;
        modal.classList.add('active');
    },

    showCloseSessionModal() {
        const modal = document.getElementById('modal-session');
        const title = document.getElementById('session-modal-title');
        const body = document.getElementById('session-modal-body');
        const footer = document.getElementById('session-modal-footer');

        const s = this.currentSession;
        const expectedBalance = (s.openingBalance || 0) + (s.totalSales || 0);

        title.textContent = 'Fechar Caixa';
        body.innerHTML = `
            <div style="margin-bottom: 20px;">
                <div class="session-card-stats" style="margin-bottom: 16px;">
                    <div class="session-stat">
                        <div class="session-stat-value">${formatCurrency(s.openingBalance || 0)}</div>
                        <div class="session-stat-label">Fundo Caixa</div>
                    </div>
                    <div class="session-stat">
                        <div class="session-stat-value">${formatCurrency(s.totalSales || 0)}</div>
                        <div class="session-stat-label">Vendas</div>
                    </div>
                    <div class="session-stat">
                        <div class="session-stat-value">${s.totalTransactions || 0}</div>
                        <div class="session-stat-label">Transações</div>
                    </div>
                    <div class="session-stat">
                        <div class="session-stat-value text-success">${formatCurrency(expectedBalance)}</div>
                        <div class="session-stat-label">Total Esperado</div>
                    </div>
                </div>
            </div>
            <div class="form-group">
                <label>Notas (opcional)</label>
                <div class="input-with-keyboard">
                    <textarea id="close-notes" class="form-input" rows="3" placeholder="Observações sobre o fecho de caixa..."></textarea>
                    <button class="btn-keyboard" data-target="close-notes" data-label="Notas">⌨</button>
                </div>
            </div>
        `;
        footer.innerHTML = `
            <button class="btn btn-secondary" onclick="app.closeSessionModal()">Cancelar</button>
            <button class="btn btn-danger" onclick="app.closeSession()">Fechar Caixa</button>
        `;
        modal.classList.add('active');
    },

    closeSessionModal() {
        document.getElementById('modal-session').classList.remove('active');
    },

    async openSession() {
        const btn = document.querySelector('#session-modal-footer .btn-success');
        setButtonLoading(btn, true);
        try {
            const balance = parseFloat(document.getElementById('opening-balance').value) || 0;
            this.currentSession = await bridge.send('openCashSession', { openingBalance: balance });
            this.updateSessionUI();
            this.closeSessionModal();
            showToast('Caixa aberta com sucesso!', 'success');
            if (this.currentPage === 'history') history.showTab(history.currentTab);
        } catch (e) {
            showToast('Erro ao abrir caixa: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async closeSession() {
        const btn = document.querySelector('#session-modal-footer .btn-danger');
        setButtonLoading(btn, true);
        try {
            const notes = document.getElementById('close-notes').value;
            await bridge.send('closeCashSession', { notes });
            this.currentSession = null;
            this.updateSessionUI();
            this.closeSessionModal();
            showToast('Caixa fechada com sucesso! Relatório impresso.', 'success');
            if (this.currentPage === 'history') history.showTab(history.currentTab);
        } catch (e) {
            showToast('Erro ao fechar caixa: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    closeFormModal() {
        document.getElementById('modal-form').classList.remove('active');
    }
};

// ===== Utility Functions =====
function formatCurrency(value) {
    return Number(value).toFixed(2).replace('.', ',') + ' €';
}

function formatDateTime(dateStr) {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) {
        // Try parsing as "YYYY-MM-DD HH:MM:SS"
        return dateStr;
    }
    return d.toLocaleString('pt-PT', { 
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function formatDate(dateStr) {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return dateStr;
    return d.toLocaleDateString('pt-PT');
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('toast-exit');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function setButtonLoading(btn, loading) {
    if (!btn) return;
    if (btn.type === 'checkbox') {
        btn.disabled = loading;
        return;
    }
    if (loading) {
        btn.disabled = true;
        btn.dataset.origContent = btn.innerHTML;
        btn.innerHTML = '<span class="btn-spinner"></span>';
    } else {
        btn.disabled = false;
        if (btn.dataset.origContent !== undefined) {
            btn.innerHTML = btn.dataset.origContent;
            delete btn.dataset.origContent;
        }
    }
}

function showPaymentSuccess(amount) {
    const overlay = document.createElement('div');
    overlay.className = 'payment-success-overlay';
    overlay.innerHTML = `
        <div class="check-icon">✓</div>
        <div class="success-amount">${formatCurrency(amount)}</div>
        <div class="success-text">Pagamento registado com sucesso</div>
    `;
    document.body.appendChild(overlay);

    setTimeout(() => {
        overlay.style.animation = 'fadeIn 0.3s ease reverse';
        setTimeout(() => overlay.remove(), 300);
    }, 1500);
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => app.init());
