// ===== History: Transaction & Session History =====
const history = {
    currentTab: 'transactions',
    transactions: [],
    sessions: [],

    async init() {
        this.showTab(this.currentTab);
    },

    async showTab(tab) {
        this.currentTab = tab;

        document.querySelectorAll('.history-tabs .tab-btn').forEach(btn => {
            btn.classList.toggle('active', btn.textContent.toLowerCase().includes(
                tab === 'transactions' ? 'transaç' : 'sessõ'
            ));
        });

        const content = document.getElementById('history-content');

        if (tab === 'transactions') {
            await this.renderTransactions(content);
        } else {
            await this.renderSessions(content);
        }
    },

    async renderTransactions(container) {
        const today = new Date().toISOString().split('T')[0];

        container.innerHTML = `
            <div class="date-filter">
                <label>De:</label>
                <input type="date" id="filter-from" value="${today}" onchange="history.filterTransactions()">
                <label>Até:</label>
                <input type="date" id="filter-to" value="${today}" onchange="history.filterTransactions()">
                <button class="btn btn-small btn-outline" onclick="history.filterTransactions(this)">Filtrar</button>
            </div>
            <div id="transactions-list"></div>`;

        await this.filterTransactions();
    },

    async filterTransactions(btn) {
        const from = document.getElementById('filter-from').value;
        const to = document.getElementById('filter-to').value;
        const listEl = document.getElementById('transactions-list');

        setButtonLoading(btn, true);
        try {
            this.transactions = await bridge.send('getTransactions', { dateFrom: from, dateTo: to });
        } catch (e) {
            this.transactions = [];
        } finally {
            setButtonLoading(btn, false);
        }

        if (!this.transactions || this.transactions.length === 0) {
            listEl.innerHTML = `
                <div class="empty-state">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
                    <p>Sem transações neste período</p>
                </div>`;
            return;
        }

        // Summary
        const validTrans = this.transactions.filter(t => !t.voided);
        const totalSales = validTrans.reduce((s, t) => s + t.totalAmount, 0);
        const voidedCount = this.transactions.filter(t => t.voided).length;

        let html = `
            <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 16px;">
                <div class="session-card" style="padding: 12px; margin: 0;">
                    <div class="session-stat">
                        <div class="session-stat-value">${validTrans.length}</div>
                        <div class="session-stat-label">Transações</div>
                    </div>
                </div>
                <div class="session-card" style="padding: 12px; margin: 0;">
                    <div class="session-stat">
                        <div class="session-stat-value text-success">${formatCurrency(totalSales)}</div>
                        <div class="session-stat-label">Total Vendas</div>
                    </div>
                </div>
                <div class="session-card" style="padding: 12px; margin: 0;">
                    <div class="session-stat">
                        <div class="session-stat-value ${voidedCount > 0 ? 'text-danger' : ''}">${voidedCount}</div>
                        <div class="session-stat-label">Anuladas</div>
                    </div>
                </div>
            </div>`;

        this.transactions.forEach(t => {
            const paymentLabel = { Cash: 'Dinheiro', Card: 'Cartão', MBWay: 'MB Way' }[t.paymentMethod] || t.paymentMethod;

            html += `
                <div class="transaction-card ${t.voided ? 'voided' : ''}" onclick="history.toggleDetails(${t.id})">
                    <div class="transaction-header">
                        <span class="transaction-number">Talão #${t.transactionNumber}</span>
                        <span class="transaction-amount ${t.voided ? 'voided' : ''}">${formatCurrency(t.totalAmount)}</span>
                    </div>
                    <div class="transaction-meta">
                        <span>${formatDateTime(t.createdAt)}</span>
                        <span>${paymentLabel}</span>
                        <span>Sessão #${t.cashSessionId}</span>
                        ${t.voided ? '<span class="text-danger">ANULADA</span>' : ''}
                    </div>
                    <div class="transaction-details" id="details-${t.id}">`;

            if (t.items && t.items.length > 0) {
                t.items.forEach(item => {
                    html += `
                        <div class="transaction-detail-item">
                            <span>${item.productName} x${item.quantity}</span>
                            <span>${formatCurrency(item.totalPrice)}</span>
                        </div>`;
                });
            }

            if (!t.voided) {
                html += `
                    <div class="transaction-actions">
                        <button class="btn btn-danger btn-small" onclick="event.stopPropagation(); history.voidTransaction(${t.id}, this)">Anular</button>
                        <button class="btn btn-outline btn-small" onclick="event.stopPropagation(); history.reprintTransaction(${t.id}, this)">Reimprimir</button>
                    </div>`;
            }

            html += `</div></div>`;
        });

        listEl.innerHTML = html;
    },

    toggleDetails(id) {
        const el = document.getElementById(`details-${id}`);
        if (el) el.classList.toggle('open');
    },

    async voidTransaction(id, btn) {
        if (!confirm('Tem a certeza que pretende anular esta transação?')) return;

        setButtonLoading(btn, true);
        try {
            await bridge.send('voidTransaction', { id });
            showToast('Transação anulada', 'warning');

            // Refresh session data
            await app.checkSession();
            app.updateSessionUI();

            await this.filterTransactions();
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async reprintTransaction(id, btn) {
        setButtonLoading(btn, true);
        try {
            await bridge.send('reprintTransaction', { id });
            showToast('Talão enviado para impressão', 'success');
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    async reprintSession(id, btn) {
        setButtonLoading(btn, true);
        try {
            await bridge.send('reprintSession', { id });
            showToast('Relatório enviado para impressão', 'success');
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    // ===== Cash Sessions =====
    async renderSessions(container) {
        let rawSessions;
        try {
            rawSessions = await bridge.send('getCashSessions');
        } catch (e) {
            rawSessions = [];
        }

        this.sessions = rawSessions || [];

        if (this.sessions.length === 0) {
            container.innerHTML = `
                <div class="empty-state">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
                    <p>Sem sessões de caixa registadas</p>
                </div>`;
            return;
        }

        const paymentLabels = { Cash: 'Dinheiro', Card: 'Cartão', MBWay: 'MB Way' };

        let html = '';
        this.sessions.forEach(detail => {
            const s = detail.session;
            const breakdown = detail.paymentBreakdown || [];
            const isOpen = s.status === 'Open';
            const totalCaixa = s.closingBalance ?? ((s.openingBalance || 0) + (s.totalSales || 0));

            let breakdownHtml = '';
            if (breakdown.length > 0) {
                breakdownHtml = `<div class="session-payment-breakdown">
                    <div class="session-breakdown-label">Pagamentos</div>`;
                breakdown.forEach(pm => {
                    const label = paymentLabels[pm.method] || pm.method;
                    breakdownHtml += `<div class="session-breakdown-row">
                        <span>${label} (${pm.count})</span>
                        <span>${formatCurrency(pm.total)}</span>
                    </div>`;
                });
                breakdownHtml += `</div>`;
            }

            html += `
                <div class="session-card">
                    <div class="session-card-header">
                        <span class="session-card-title">Sessão #${s.id}</span>
                        <span class="session-status ${isOpen ? 'open' : 'closed'}">${isOpen ? 'Aberta' : 'Fechada'}</span>
                    </div>
                    <div style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px;">
                        Abertura: ${formatDateTime(s.openedAt)}${s.closedAt ? ' · Fecho: ' + formatDateTime(s.closedAt) : ''}
                    </div>
                    <div class="session-opening-balance">
                        <span class="session-stat-label">Fundo de Caixa</span>
                        <span class="session-stat-value">${formatCurrency(s.openingBalance || 0)}</span>
                    </div>
                    ${breakdownHtml}
                    <div class="session-totals">
                        <div class="session-total-row">
                            <span>Total Vendas</span>
                            <span class="text-success">${formatCurrency(s.totalSales || 0)}</span>
                        </div>
                        ${!isOpen ? `<div class="session-total-row session-total-caixa">
                            <span>Total Caixa</span>
                            <span class="text-success">${formatCurrency(totalCaixa)}</span>
                        </div>` : ''}
                    </div>
                    ${s.notes ? `<div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border); font-size: 13px; color: var(--text-light);">Notas: ${s.notes}</div>` : ''}
                    ${!isOpen ? `
                        <div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border);">
                            <button class="btn btn-outline btn-small" onclick="history.reprintSession(${s.id}, this)">Reimprimir Relatório</button>
                        </div>` : ''}
                </div>`;
        });

        container.innerHTML = html;
    }
};
