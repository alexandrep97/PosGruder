// ===== Settings: Configuration Management =====
const settings = {
    currentTab: 'categories',
    categories: [],
    products: [],

    async init() {
        this.showTab(this.currentTab);
    },

    async showTab(tab) {
        this.currentTab = tab;

        // Update tab buttons - match by tab data attribute
        const tabLabels = {
            categories: 'categorias',
            products: 'produtos',
            receipt: 'layout',
            printer: 'impressora',
            general: 'geral'
        };
        document.querySelectorAll('.settings-tabs .tab-btn').forEach(btn => {
            btn.classList.toggle('active', btn.textContent.toLowerCase().includes(tabLabels[tab] || tab));
        });

        const content = document.getElementById('settings-content');

        switch (tab) {
            case 'categories':
                await this.renderCategories(content);
                break;
            case 'products':
                await this.renderProducts(content);
                break;
            case 'receipt':
                await this.renderReceiptLayout(content);
                break;
            case 'printer':
                await this.renderPrinter(content);
                break;
            case 'general':
                await this.renderGeneral(content);
                break;
        }
    },

    // ===== Categories =====
    async renderCategories(container) {
        try {
            this.categories = await bridge.send('getAllCategories');
        } catch (e) {
            this.categories = [];
        }

        let html = `
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                <h3 style="font-size: 16px;">Categorias</h3>
                <button class="btn btn-primary btn-small" onclick="settings.showCategoryForm()">+ Nova Categoria</button>
            </div>
            <div class="settings-list">`;

        this.categories.forEach((cat, i) => {
            html += `
                <div class="settings-item" ${!cat.isActive ? 'style="opacity:0.5"' : ''}>
                    <div style="display:flex; flex-direction:column; gap:4px;">
                        <button class="btn-reorder" onclick="settings.moveCategory(${i}, -1)" ${i === 0 ? 'disabled style="opacity:0.3"' : ''}>▲</button>
                        <button class="btn-reorder" onclick="settings.moveCategory(${i}, 1)" ${i === this.categories.length - 1 ? 'disabled style="opacity:0.3"' : ''}>▼</button>
                    </div>
                    <div class="settings-item-info" style="flex:1;">
                        <div class="settings-item-name">${cat.name}</div>
                        <div class="settings-item-detail">Ordem: ${cat.sortOrder}</div>
                    </div>
                    <div style="display:flex; align-items:center; gap:8px;">
                        <label class="toggle-switch" title="${cat.isActive ? 'Ativa' : 'Inativa'}">
                            <input type="checkbox" ${cat.isActive ? 'checked' : ''} onchange="settings.toggleCategoryActive(${cat.id}, this.checked, this)">
                            <span class="toggle-slider"></span>
                        </label>
                    </div>
                    <div class="settings-item-actions">
                        <button class="btn btn-outline btn-small" onclick="settings.showCategoryForm(${cat.id})">Editar</button>
                        <button class="btn btn-danger btn-small" onclick="settings.deleteCategory(${cat.id}, this)">Remover</button>
                    </div>
                </div>`;
        });

        html += '</div>';
        container.innerHTML = html;
    },

    async toggleCategoryActive(id, isActive, el) {
        const cat = this.categories.find(c => c.id === id);
        if (!cat) return;
        setButtonLoading(el, true);
        try {
            await bridge.send('saveCategory', {
                data: { id: cat.id, name: cat.name, isActive, sortOrder: cat.sortOrder }
            });
            showToast(isActive ? 'Categoria ativada' : 'Categoria desativada', 'success');
            await this.renderCategories(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(el, false);
        }
    },

    showCategoryForm(id = null) {
        const cat = id ? this.categories.find(c => c.id === id) : null;
        const modal = document.getElementById('modal-form');
        const title = document.getElementById('form-modal-title');
        const body = document.getElementById('form-modal-body');
        const footer = document.getElementById('form-modal-footer');

        title.textContent = cat ? 'Editar Categoria' : 'Nova Categoria';
        body.innerHTML = `
            <div class="form-group">
                <label>Nome da Categoria</label>
                <div class="input-with-keyboard"><input type="text" id="cat-name" class="form-input" value="${cat ? cat.name : ''}" placeholder="Ex: Bebidas, Comida..."><button class="btn-keyboard" data-target="cat-name" data-label="Nome da Categoria">⌨</button></div>
            </div>
            <div class="form-group">
                <label class="form-checkbox">
                    <input type="checkbox" id="cat-active" ${!cat || cat.isActive ? 'checked' : ''}>
                    <span>Categoria ativa</span>
                </label>
            </div>`;
        footer.innerHTML = `
            <button class="btn btn-secondary" onclick="app.closeFormModal()">Cancelar</button>
            <button class="btn btn-primary" onclick="settings.saveCategory(${id || 'null'}, this)">Guardar</button>
        `;
        modal.classList.add('active');
        document.getElementById('cat-name').focus();
    },

    async saveCategory(id, btn) {
        const name = document.getElementById('cat-name').value.trim();
        const isActive = document.getElementById('cat-active').checked;

        if (!name) {
            showToast('Introduza o nome da categoria', 'warning');
            return;
        }

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveCategory', {
                data: { id, name, isActive, sortOrder: 0 }
            });
            app.closeFormModal();
            showToast('Categoria guardada!', 'success');
            await this.renderCategories(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async deleteCategory(id, btn) {
        if (!confirm('Tem a certeza que pretende remover esta categoria?')) return;
        setButtonLoading(btn, true);
        try {
            await bridge.send('deleteCategory', { id });
            showToast('Categoria removida', 'success');
            await this.renderCategories(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async moveCategory(index, direction) {
        const newIndex = index + direction;
        if (newIndex < 0 || newIndex >= this.categories.length) return;

        const ids = this.categories.map(c => c.id);
        [ids[index], ids[newIndex]] = [ids[newIndex], ids[index]];

        try {
            await bridge.send('reorderCategories', { ids });
            await this.renderCategories(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro ao reordenar', 'error');
        }
    },

    // ===== Products =====
    async renderProducts(container) {
        try {
            this.products = await bridge.send('getAllProducts');
            this.categories = await bridge.send('getAllCategories');
        } catch (e) {
            this.products = [];
        }

        let html = `
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                <h3 style="font-size: 16px;">Produtos</h3>
                <button class="btn btn-primary btn-small" onclick="settings.showProductForm()">+ Novo Produto</button>
            </div>`;

        // Group by category
        const grouped = {};
        this.categories.forEach(c => grouped[c.id] = { category: c, products: [] });
        this.products.forEach(p => {
            if (grouped[p.categoryId]) grouped[p.categoryId].products.push(p);
        });

        Object.values(grouped).forEach(group => {
            if (group.products.length === 0) return;
            html += `<h4 style="font-size: 14px; color: var(--text-light); margin: 16px 0 8px; padding-left: 4px;">${group.category.name}</h4>
            <div class="settings-list">`;

            group.products.forEach(prod => {
                html += `
                    <div class="settings-item" ${!prod.isActive ? 'style="opacity:0.5"' : ''}>
                        <div class="settings-item-info" style="flex:1;">
                            <div class="settings-item-name">${prod.name} ${prod.isGeneric ? '<span style="font-size:11px; color:var(--warning);">(Genérico)</span>' : ''}</div>
                            <div class="settings-item-detail">${prod.isGeneric ? 'Valor manual' : formatCurrency(prod.price)}</div>
                        </div>
                        <div style="display:flex; align-items:center; gap:8px;">
                            <label class="toggle-switch" title="${prod.isActive ? 'Ativo' : 'Inativo'}">
                                <input type="checkbox" ${prod.isActive ? 'checked' : ''} onchange="settings.toggleProductActive(${prod.id}, this.checked, this)">
                                <span class="toggle-slider"></span>
                            </label>
                        </div>
                        <div class="settings-item-actions">
                            <button class="btn btn-outline btn-small" onclick="settings.showProductForm(${prod.id})">Editar</button>
                            <button class="btn btn-danger btn-small" onclick="settings.deleteProduct(${prod.id}, this)">Remover</button>
                        </div>
                    </div>`;
            });

            html += '</div>';
        });

        container.innerHTML = html;
    },

    async toggleProductActive(id, isActive, el) {
        const prod = this.products.find(p => p.id === id);
        if (!prod) return;
        setButtonLoading(el, true);
        try {
            await bridge.send('saveProduct', {
                data: {
                    id: prod.id, name: prod.name, categoryId: prod.categoryId,
                    price: prod.price, isGeneric: prod.isGeneric, isActive, sortOrder: prod.sortOrder
                }
            });
            showToast(isActive ? 'Produto ativado' : 'Produto desativado', 'success');
            await this.renderProducts(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(el, false);
        }
    },

    showProductForm(id = null) {
        const prod = id ? this.products.find(p => p.id === id) : null;
        const modal = document.getElementById('modal-form');
        const title = document.getElementById('form-modal-title');
        const body = document.getElementById('form-modal-body');
        const footer = document.getElementById('form-modal-footer');

        let catOptions = this.categories.map(c =>
            `<option value="${c.id}" ${prod && prod.categoryId === c.id ? 'selected' : ''}>${c.name}</option>`
        ).join('');

        title.textContent = prod ? 'Editar Produto' : 'Novo Produto';
        body.innerHTML = `
            <div class="form-group">
                <label>Nome do Produto</label>
                <div class="input-with-keyboard"><input type="text" id="prod-name" class="form-input" value="${prod ? prod.name : ''}" placeholder="Ex: Bifana, Cerveja..."><button class="btn-keyboard" data-target="prod-name" data-label="Nome do Produto">⌨</button></div>
            </div>
            <div class="form-group">
                <label>Categoria</label>
                <select id="prod-category" class="form-select">${catOptions}</select>
            </div>
            <div class="form-group">
                <label>Preço (€)</label>
                <input type="number" id="prod-price" class="form-input" value="${prod ? prod.price : ''}" step="0.01" min="0" placeholder="0.00">
            </div>
            <div class="form-group">
                <label class="form-checkbox">
                    <input type="checkbox" id="prod-generic" ${prod && prod.isGeneric ? 'checked' : ''} onchange="document.getElementById('prod-price').disabled = this.checked;">
                    <span>Artigo genérico (valor manual)</span>
                </label>
            </div>
            <div class="form-group">
                <label class="form-checkbox">
                    <input type="checkbox" id="prod-active" ${!prod || prod.isActive ? 'checked' : ''}>
                    <span>Produto ativo</span>
                </label>
            </div>`;
        footer.innerHTML = `
            <button class="btn btn-secondary" onclick="app.closeFormModal()">Cancelar</button>
            <button class="btn btn-primary" onclick="settings.saveProduct(${id || 'null'}, this)">Guardar</button>
        `;
        modal.classList.add('active');
        document.getElementById('prod-name').focus();

        if (prod && prod.isGeneric) {
            document.getElementById('prod-price').disabled = true;
        }
    },

    async saveProduct(id, btn) {
        const name = document.getElementById('prod-name').value.trim();
        const categoryId = parseInt(document.getElementById('prod-category').value);
        const price = parseFloat(document.getElementById('prod-price').value) || 0;
        const isGeneric = document.getElementById('prod-generic').checked;
        const isActive = document.getElementById('prod-active').checked;

        if (!name) {
            showToast('Introduza o nome do produto', 'warning');
            return;
        }

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveProduct', {
                data: { id, name, categoryId, price: isGeneric ? 0 : price, isGeneric, isActive, sortOrder: 0 }
            });
            app.closeFormModal();
            showToast('Produto guardado!', 'success');
            await this.renderProducts(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    async deleteProduct(id, btn) {
        if (!confirm('Tem a certeza que pretende remover este produto?')) return;
        setButtonLoading(btn, true);
        try {
            await bridge.send('deleteProduct', { id });
            showToast('Produto removido', 'success');
            await this.renderProducts(document.getElementById('settings-content'));
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
            setButtonLoading(btn, false);
        }
    },

    // ==========================================================
    // ===== LAYOUT DO TALÃO (Nova Tab) =========================
    // ==========================================================
    async renderReceiptLayout(container) {
        let s = {};
        try {
            s = await bridge.send('getSettings');
        } catch (e) {}

        const printMode = s.PrintMode || 'Complete';
        const headerEnabled = s.HeaderEnabled !== 'false';
        const footerEnabled = s.FooterEnabled !== 'false';
        const bodyEnabled = s.BodyEnabled !== 'false';
        const h1 = s.HeaderLine1 || '';
        const h2 = s.HeaderLine2 || '';
        const h3 = s.HeaderLine3 || '';
        const h4 = s.HeaderLine4 || '';
        const f1 = s.FooterLine1 || '';
        const f2 = s.FooterLine2 || '';
        const bt = s.BodyTitle || '';
        const b1 = s.BodyLine1 || '';
        const b2 = s.BodyLine2 || '';
        const showDate = s.ShowDate !== 'false';
        const showSession = s.ShowSession !== 'false';
        const showReceipt = s.ShowReceiptNumber !== 'false';
        const showTicket = s.ShowTicketNumber !== 'false';
        const showGrid = s.ShowGridHeader !== 'false';
        const showPayment = s.ShowPaymentMethod !== 'false';
        const showTotals = s.ShowTotals !== 'false';

        const makeSwitch = (id, label, checked, hint) => `
            <div style="display:flex; justify-content:space-between; align-items:center; padding:8px 0; border-bottom:1px solid var(--bg-dark);">
                <div>
                    <span style="font-size:13px; font-weight:600;">${label}</span>
                    ${hint ? `<div style="font-size:11px; color:var(--text-muted);">${hint}</div>` : ''}
                </div>
                <label class="toggle-switch">
                    <input type="checkbox" id="${id}" ${checked ? 'checked' : ''} onchange="settings.updateReceiptPreview()">
                    <span class="toggle-slider"></span>
                </label>
            </div>`;

        container.innerHTML = `
            <div class="settings-form">
                <h3 style="font-size: 16px; margin-bottom: 8px;">Modo de Impressão</h3>
                <p style="font-size: 13px; color: var(--text-muted); margin-bottom: 16px;">
                    Define como os artigos são impressos em cada transação.
                </p>
                <div class="print-mode-cards">
                    <div class="print-mode-card ${printMode === 'Complete' ? 'active' : ''}" onclick="settings.selectPrintMode('Complete')">
                        <div class="print-mode-icon">🧾</div>
                        <div class="print-mode-title">Talão Completo</div>
                        <div class="print-mode-desc">
                            Um único talão com todos os artigos, quantidades, totais e método de pagamento.
                            <br><br><em>Ex: 4 cervejas + 2 bifanas = 1 talão com a lista completa</em>
                        </div>
                    </div>
                    <div class="print-mode-card ${printMode === 'Individual' ? 'active' : ''}" onclick="settings.selectPrintMode('Individual')">
                        <div class="print-mode-icon">🎟️</div>
                        <div class="print-mode-title">Senhas Individuais</div>
                        <div class="print-mode-desc">
                            Uma senha separada por cada unidade de artigo, com corte entre senhas.
                            <br><br><em>Ex: 4 cervejas + 2 bifanas = 6 senhas com corte</em>
                        </div>
                    </div>
                </div>
                <input type="hidden" id="receipt-print-mode" value="${printMode}">
            </div>

            <!-- CABEÇALHO -->
            <div class="settings-form" style="margin-top: 16px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                    <h3 style="font-size: 16px;">Cabeçalho do Talão</h3>
                    <label class="toggle-switch">
                        <input type="checkbox" id="receipt-header-enabled" ${headerEnabled ? 'checked' : ''} onchange="settings.toggleReceiptSection('header', this.checked)">
                        <span class="toggle-slider"></span>
                    </label>
                </div>
                <div id="receipt-header-fields" ${!headerEnabled ? 'style="opacity:0.4; pointer-events:none;"' : ''}>
                    <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px;">
                        Linha 1 é impressa em tamanho grande e negrito. Linhas vazias são omitidas.
                    </p>
                    <div class="form-group"><label>Linha 1 (Título principal)</label><div class="input-with-keyboard"><input type="text" id="receipt-h1" class="form-input" value="${h1}" placeholder="Ex: GRUDER"><button class="btn-keyboard" data-target="receipt-h1" data-label="Cabeçalho Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha 2</label><div class="input-with-keyboard"><input type="text" id="receipt-h2" class="form-input" value="${h2}" placeholder="Ex: GRUPO DESPORTIVO DA"><button class="btn-keyboard" data-target="receipt-h2" data-label="Cabeçalho Linha 2">⌨</button></div></div>
                    <div class="form-group"><label>Linha 3</label><div class="input-with-keyboard"><input type="text" id="receipt-h3" class="form-input" value="${h3}" placeholder="Ex: RIBEIRA DO FARRIO"><button class="btn-keyboard" data-target="receipt-h3" data-label="Cabeçalho Linha 3">⌨</button></div></div>
                    <div class="form-group"><label>Linha 4</label><div class="input-with-keyboard"><input type="text" id="receipt-h4" class="form-input" value="${h4}" placeholder="Ex: Fundado em 1977"><button class="btn-keyboard" data-target="receipt-h4" data-label="Cabeçalho Linha 4">⌨</button></div></div>
                </div>
            </div>

            <!-- CORPO -->
            <div class="settings-form" style="margin-top: 16px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                    <h3 style="font-size: 16px;">Corpo do Talão</h3>
                    <label class="toggle-switch">
                        <input type="checkbox" id="receipt-body-enabled" ${bodyEnabled ? 'checked' : ''} onchange="settings.toggleReceiptSection('body', this.checked)">
                        <span class="toggle-slider"></span>
                    </label>
                </div>
                <div id="receipt-body-fields" ${!bodyEnabled ? 'style="opacity:0.4; pointer-events:none;"' : ''}>
                    <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px;">
                        Textos personalizados exibidos no corpo do talão. Se vazio, usa o nome do evento das definições gerais.
                    </p>
                    <div class="form-group"><label>Título do Corpo (negrito)</label><div class="input-with-keyboard"><input type="text" id="receipt-bt" class="form-input" value="${bt}" placeholder="Ex: Festa GRUDER 2026"><button class="btn-keyboard" data-target="receipt-bt" data-label="Título do Corpo">⌨</button></div></div>
                    <div class="form-group"><label>Linha adicional 1</label><div class="input-with-keyboard"><input type="text" id="receipt-b1" class="form-input" value="${b1}" placeholder="Ex: Bar Principal"><button class="btn-keyboard" data-target="receipt-b1" data-label="Corpo Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha adicional 2</label><div class="input-with-keyboard"><input type="text" id="receipt-b2" class="form-input" value="${b2}" placeholder=""><button class="btn-keyboard" data-target="receipt-b2" data-label="Corpo Linha 2">⌨</button></div></div>
                </div>

                <h4 style="font-size: 14px; margin: 20px 0 8px; color: var(--text-light);">Campos Visíveis</h4>
                <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 8px;">Ative ou desative cada campo no corpo do talão.</p>
                ${makeSwitch('receipt-show-date', 'Data e Hora', showDate, '')}
                ${makeSwitch('receipt-show-session', 'Nº Sessão', showSession, '')}
                ${makeSwitch('receipt-show-receipt', 'Nº Talão', showReceipt, '')}
                ${makeSwitch('receipt-show-ticket', 'Nº Senha', showTicket, 'Apenas em senhas individuais')}
                ${makeSwitch('receipt-show-grid', 'Cabeçalho da Grelha', showGrid, 'Apenas em talão completo (Artigo / Qtd / Total)')}
                ${makeSwitch('receipt-show-payment', 'Modo de Pagamento', showPayment, '')}
                ${makeSwitch('receipt-show-totals', 'Totais / Preços', showTotals, '')}
            </div>

            <!-- RODAPÉ -->
            <div class="settings-form" style="margin-top: 16px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                    <h3 style="font-size: 16px;">Rodapé do Talão</h3>
                    <label class="toggle-switch">
                        <input type="checkbox" id="receipt-footer-enabled" ${footerEnabled ? 'checked' : ''} onchange="settings.toggleReceiptSection('footer', this.checked)">
                        <span class="toggle-slider"></span>
                    </label>
                </div>
                <div id="receipt-footer-fields" ${!footerEnabled ? 'style="opacity:0.4; pointer-events:none;"' : ''}>
                    <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px;">Linha 2 é impressa em negrito. Linhas vazias são omitidas.</p>
                    <div class="form-group"><label>Linha 1</label><div class="input-with-keyboard"><input type="text" id="receipt-f1" class="form-input" value="${f1}" placeholder="Ex: Obrigado pela preferência!"><button class="btn-keyboard" data-target="receipt-f1" data-label="Rodapé Linha 1">⌨</button></div></div>
                    <div class="form-group"><label>Linha 2 (Destaque)</label><div class="input-with-keyboard"><input type="text" id="receipt-f2" class="form-input" value="${f2}" placeholder="Ex: GRUDER - 1977"><button class="btn-keyboard" data-target="receipt-f2" data-label="Rodapé Linha 2">⌨</button></div></div>
                </div>
            </div>

            <!-- PREVIEW -->
            <div class="settings-form" style="margin-top: 16px;">
                <h3 style="font-size: 16px; margin-bottom: 12px;">Pré-visualização</h3>
                <div id="receipt-preview" class="receipt-preview"></div>
            </div>

            <!-- GUARDAR -->
            <div style="margin-top: 16px; display: flex; gap: 8px;">
                <button class="btn btn-primary" onclick="settings.saveReceiptLayout(this)">Guardar Layout</button>
                <button class="btn btn-outline" onclick="settings.renderReceiptLayout(document.getElementById('settings-content'))">Cancelar Alterações</button>
            </div>
        `;

        this.updateReceiptPreview();

        // Live preview on any input change
        ['receipt-h1', 'receipt-h2', 'receipt-h3', 'receipt-h4', 'receipt-f1', 'receipt-f2', 'receipt-bt', 'receipt-b1', 'receipt-b2'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.addEventListener('input', () => settings.updateReceiptPreview());
        });
    },

    selectPrintMode(mode) {
        document.getElementById('receipt-print-mode').value = mode;
        document.querySelectorAll('.print-mode-card').forEach(card => {
            card.classList.toggle('active', card.querySelector('.print-mode-title').textContent.includes(
                mode === 'Complete' ? 'Completo' : 'Individuais'
            ));
        });
        this.updateReceiptPreview();
    },

    toggleReceiptSection(section, enabled) {
        const fields = document.getElementById(`receipt-${section}-fields`);
        if (fields) {
            fields.style.opacity = enabled ? '1' : '0.4';
            fields.style.pointerEvents = enabled ? 'auto' : 'none';
        }
        this.updateReceiptPreview();
    },

    updateReceiptPreview() {
        const preview = document.getElementById('receipt-preview');
        if (!preview) return;

        const mode = document.getElementById('receipt-print-mode')?.value || 'Complete';
        const headerOn = document.getElementById('receipt-header-enabled')?.checked ?? true;
        const bodyOn = document.getElementById('receipt-body-enabled')?.checked ?? true;
        const footerOn = document.getElementById('receipt-footer-enabled')?.checked ?? true;
        const h1 = document.getElementById('receipt-h1')?.value || '';
        const h2 = document.getElementById('receipt-h2')?.value || '';
        const h3 = document.getElementById('receipt-h3')?.value || '';
        const h4 = document.getElementById('receipt-h4')?.value || '';
        const f1 = document.getElementById('receipt-f1')?.value || '';
        const f2 = document.getElementById('receipt-f2')?.value || '';
        const bt = document.getElementById('receipt-bt')?.value || '';
        const b1 = document.getElementById('receipt-b1')?.value || '';
        const b2 = document.getElementById('receipt-b2')?.value || '';
        const showDate = document.getElementById('receipt-show-date')?.checked ?? true;
        const showSession = document.getElementById('receipt-show-session')?.checked ?? true;
        const showReceipt = document.getElementById('receipt-show-receipt')?.checked ?? true;
        const showTicket = document.getElementById('receipt-show-ticket')?.checked ?? true;
        const showGrid = document.getElementById('receipt-show-grid')?.checked ?? true;
        const showPayment = document.getElementById('receipt-show-payment')?.checked ?? true;
        const showTotals = document.getElementById('receipt-show-totals')?.checked ?? true;

        // Resolve body title: custom or fallback to event name
        const bodyTitle = bt || 'Festa GRUDER 2026';

        const addHeader = (lines) => {
            if (!headerOn) return;
            lines.push('==========================================');
            if (h1) lines.push(`<b class="big">${h1}</b>`);
            if (h2) lines.push(`<b>${h2}</b>`);
            if (h3) lines.push(`<b>${h3}</b>`);
            if (h4) lines.push(h4);
            lines.push('==========================================');
        };

        const addFooter = (lines) => {
            if (!footerOn) return;
            if (f1) { lines.push(''); lines.push(f1); }
            if (f2) lines.push(`<b>${f2}</b>`);
        };

        let lines = [];

        if (mode === 'Complete') {
            addHeader(lines);
            if (bodyOn) {
                lines.push(`<b>${bodyTitle}</b>`);
                if (b1) lines.push(b1);
                if (b2) lines.push(b2);
            }
            lines.push('------------------------------------------');
            if (showDate) lines.push('Data: 10/04/2026 17:30');
            if (showReceipt) lines.push('Talao No: 42');
            if (showSession) lines.push('Sessao:   #1');
            lines.push('==========================================');
            if (showGrid) {
                lines.push('<b>Artigo              Qtd   Total</b>');
                lines.push('------------------------------------------');
            }
            lines.push('Cerveja              x4    6.00');
            lines.push('Bifana               x2    6.00');
            lines.push('------------------------------------------');
            if (showTotals) lines.push('<b class="big">TOTAL:              12.00 EUR</b>');
            if (showPayment) lines.push('Pagamento: Dinheiro');
            lines.push('==========================================');
            addFooter(lines);
            lines.push('<span class="cut">--- ✂ corte ---</span>');
        } else {
            for (let ticket = 1; ticket <= 2; ticket++) {
                const isFirst = ticket === 1;
                const name = isFirst ? 'Cerveja' : 'Bifana';
                const price = isFirst ? '1.50' : '3.00';
                const num = isFirst ? '1/6' : '5/6';

                addHeader(lines);
                if (bodyOn) {
                    lines.push(`<b>${bodyTitle}</b>`);
                    if (b1) lines.push(b1);
                    if (b2) lines.push(b2);
                }
                lines.push('------------------------------------------');
                if (showDate) lines.push('Data: 10/04/2026 17:30');
                let infoLine = [];
                if (showReceipt) infoLine.push('Talao: 42');
                if (showTicket) infoLine.push(`Senha: ${num}`);
                if (infoLine.length) lines.push(infoLine.join('  '));
                lines.push('==========================================');
                lines.push(`<b class="big">${name}</b>`);
                if (showTotals) lines.push(`<b>${price} EUR</b>`);
                if (showPayment) { lines.push(''); lines.push('Dinheiro'); }
                lines.push('==========================================');
                addFooter(lines);
                lines.push('<span class="cut">--- ✂ corte ---</span>');
                if (ticket < 2) lines.push('');
            }
            lines.push('<span style="color:var(--text-muted); font-style:italic;">... mais 4 senhas ...</span>');
        }

        preview.innerHTML = lines.map(l => `<div class="receipt-line">${l}</div>`).join('');
    },

    async saveReceiptLayout(btn) {
        const data = {
            PrintMode: document.getElementById('receipt-print-mode').value,
            HeaderEnabled: String(document.getElementById('receipt-header-enabled').checked),
            HeaderLine1: document.getElementById('receipt-h1').value,
            HeaderLine2: document.getElementById('receipt-h2').value,
            HeaderLine3: document.getElementById('receipt-h3').value,
            HeaderLine4: document.getElementById('receipt-h4').value,
            BodyEnabled: String(document.getElementById('receipt-body-enabled').checked),
            BodyTitle: document.getElementById('receipt-bt').value,
            BodyLine1: document.getElementById('receipt-b1').value,
            BodyLine2: document.getElementById('receipt-b2').value,
            ShowDate: String(document.getElementById('receipt-show-date').checked),
            ShowSession: String(document.getElementById('receipt-show-session').checked),
            ShowReceiptNumber: String(document.getElementById('receipt-show-receipt').checked),
            ShowTicketNumber: String(document.getElementById('receipt-show-ticket').checked),
            ShowGridHeader: String(document.getElementById('receipt-show-grid').checked),
            ShowPaymentMethod: String(document.getElementById('receipt-show-payment').checked),
            ShowTotals: String(document.getElementById('receipt-show-totals').checked),
            FooterEnabled: String(document.getElementById('receipt-footer-enabled').checked),
            FooterLine1: document.getElementById('receipt-f1').value,
            FooterLine2: document.getElementById('receipt-f2').value
        };

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveSettings', { data });
            showToast('Layout do talão guardado!', 'success');
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    // ===== Printer Settings =====
    async renderPrinter(container) {
        let portsData = { ports: [], currentPort: 'COM3', baudRate: 9600 };
        try {
            portsData = await bridge.send('getSerialPorts');
        } catch (e) {}

        const appSettings = await bridge.send('getSettings').catch(() => ({}));
        const printCopies = parseInt(appSettings.PrintCopies) || 1;

        let portsOptions = (portsData.ports || []).map(p =>
            `<option value="${p}" ${p === portsData.currentPort ? 'selected' : ''}>${p}</option>`
        ).join('');

        if (!portsOptions) {
            portsOptions = '<option value="COM3">COM3 (default)</option>';
        }

        container.innerHTML = `
            <div class="settings-form">
                <h3 style="font-size: 16px; margin-bottom: 20px;">Configuração da Impressora</h3>
                <p style="font-size: 13px; color: var(--text-muted); margin-bottom: 20px;">
                    Impressora: appPOS80AM3 (Porta Série, ESC/POS)
                </p>
                <div class="form-group">
                    <label>Porta COM</label>
                    <select id="printer-port" class="form-select">${portsOptions}</select>
                </div>
                <div class="form-group">
                    <label>Baud Rate</label>
                    <select id="printer-baud" class="form-select">
                        <option value="9600" ${portsData.baudRate === 9600 ? 'selected' : ''}>9600</option>
                        <option value="19200" ${portsData.baudRate === 19200 ? 'selected' : ''}>19200</option>
                        <option value="38400" ${portsData.baudRate === 38400 ? 'selected' : ''}>38400</option>
                        <option value="57600" ${portsData.baudRate === 57600 ? 'selected' : ''}>57600</option>
                        <option value="115200" ${portsData.baudRate === 115200 ? 'selected' : ''}>115200</option>
                    </select>
                </div>
                <div class="form-group">
                    <label class="form-checkbox">
                        <input type="checkbox" id="printer-enabled" ${appSettings.PrinterEnabled !== 'false' ? 'checked' : ''}>
                        <span>Impressora ativada</span>
                    </label>
                </div>
            </div>

            <div class="settings-form" style="margin-top: 16px;">
                <h3 style="font-size: 16px; margin-bottom: 8px;">Vias de Impressão</h3>
                <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 16px;">
                    Número de cópias idênticas impressas por cada pagamento.
                    Exemplo: 3 vias = 3 impressões idênticas do mesmo talão/senhas.
                </p>
                <div class="form-group">
                    <label>Quantidade de vias</label>
                    <div style="display:flex; align-items:center; gap:12px;">
                        <button class="qty-btn" onclick="settings.adjustCopies(-1)" style="width:44px;height:44px;font-size:20px;">−</button>
                        <span id="printer-copies-value" style="font-size:28px; font-weight:800; min-width:40px; text-align:center;">${printCopies}</span>
                        <button class="qty-btn" onclick="settings.adjustCopies(1)" style="width:44px;height:44px;font-size:20px;">+</button>
                    </div>
                </div>
            </div>

            <div style="display: flex; gap: 8px; margin-top: 16px;">
                <button class="btn btn-primary" onclick="settings.savePrinterSettings(this)">Guardar</button>
                <button class="btn btn-outline" onclick="settings.testPrint(this)">Teste de Impressão</button>
                <button class="btn btn-secondary" onclick="settings.refreshPorts(this)">Atualizar Portas</button>
            </div>`;
    },

    adjustCopies(delta) {
        const el = document.getElementById('printer-copies-value');
        let val = parseInt(el.textContent) || 1;
        val = Math.max(1, Math.min(10, val + delta));
        el.textContent = val;
    },

    async savePrinterSettings(btn) {
        const port = document.getElementById('printer-port').value;
        const baudRate = document.getElementById('printer-baud').value;
        const enabled = document.getElementById('printer-enabled').checked;
        const copies = document.getElementById('printer-copies-value').textContent;

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveSettings', {
                data: { SerialPort: port, BaudRate: baudRate, PrinterEnabled: String(enabled), PrintCopies: copies }
            });
            showToast('Configuração da impressora guardada!', 'success');
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    async testPrint(btn) {
        setButtonLoading(btn, true);
        try {
            const result = await bridge.send('testPrint');
            if (result.success) {
                showToast(result.message, 'success');
            } else {
                showToast(result.message, 'error');
            }
        } catch (e) {
            showToast('Erro ao testar impressão: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    async refreshPorts(btn) {
        setButtonLoading(btn, true);
        await this.renderPrinter(document.getElementById('settings-content'));
        showToast('Portas atualizadas', 'info');
    },

    // ===== General Settings =====
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

    async saveGeneralSettings(btn) {
        const eventName = document.getElementById('setting-event').value.trim();

        setButtonLoading(btn, true);
        try {
            await bridge.send('saveSettings', {
                data: { EventName: eventName }
            });
            app.settings.EventName = eventName;
            showToast('Definições guardadas!', 'success');
        } catch (e) {
            showToast('Erro: ' + e.message, 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    },

    async savePin(btn) {
        const currentInput = document.getElementById('pin-current');
        const newPin = (document.getElementById('pin-new')?.value || '').trim();
        const confirmPin = (document.getElementById('pin-confirm')?.value || '').trim();

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

        if (newPin !== confirmPin) {
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
};
