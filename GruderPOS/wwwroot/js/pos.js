// ===== POS: Point of Sale Logic =====
const pos = {
    categories: [],
    products: [],
    cart: [],
    selectedCategory: null,
    paymentMethod: 'Cash',

    async init() {
        await this.loadCategories();
        await this.loadProducts();
        this.renderCategoryTabs();
        this.renderProducts();
        this.renderCart();
    },

    async loadCategories() {
        try {
            this.categories = await bridge.send('getCategories');
        } catch (e) {
            console.error('Failed to load categories:', e);
            this.categories = [];
        }
    },

    async loadProducts() {
        try {
            this.products = await bridge.send('getAllProducts');
        } catch (e) {
            console.error('Failed to load products:', e);
            this.products = [];
        }
    },

    async refreshProducts() {
        await this.loadCategories();
        await this.loadProducts();
        this.renderCategoryTabs();
        this.renderProducts();
    },

    renderCategoryTabs() {
        const container = document.getElementById('category-tabs');
        let html = `<button class="category-tab ${!this.selectedCategory ? 'active' : ''}" onclick="pos.selectCategory(null)">Todos</button>`;

        this.categories.forEach(cat => {
            html += `<button class="category-tab ${this.selectedCategory === cat.id ? 'active' : ''}" onclick="pos.selectCategory(${cat.id})">${cat.name}</button>`;
        });

        container.innerHTML = html;
    },

    selectCategory(categoryId) {
        this.selectedCategory = categoryId;
        this.renderCategoryTabs();
        this.renderProducts();
    },

    renderProducts() {
        const container = document.getElementById('products-grid');
        let filtered = this.products;

        if (this.selectedCategory) {
            filtered = this.products.filter(p => p.categoryId === this.selectedCategory);
        }

        if (filtered.length === 0) {
            container.innerHTML = `
                <div class="empty-state" style="grid-column: 1/-1;">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48"><path d="M20 2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 18H4V4h16v16zM6 6h12v2H6V6zm0 4h12v2H6v-2zm0 4h8v2H6v-2z"/></svg>
                    <p>Sem produtos nesta categoria</p>
                </div>`;
            return;
        }

        let html = '';
        filtered.forEach(product => {
            const cartItem = this.cart.find(c => c.productId === product.id && !c.isGenericItem);
            const badge = cartItem ? `<div class="cart-badge">${cartItem.quantity}</div>` : '';
            const isGeneric = product.isGeneric;

            html += `
                <div class="product-card ${isGeneric ? 'generic' : ''}" onclick="${isGeneric ? 'pos.showGenericModal()' : `pos.addToCart(${product.id})`}">
                    ${badge}
                    <div class="product-name">${product.name}</div>
                    <div class="product-price">${isGeneric ? 'Valor Manual' : formatCurrency(product.price)}</div>
                </div>`;
        });

        container.innerHTML = html;
    },

    addToCart(productId) {
        const product = this.products.find(p => p.id === productId);
        if (!product) return;

        const existing = this.cart.find(c => c.productId === productId && !c.isGenericItem);
        if (existing) {
            existing.quantity++;
            existing.totalPrice = existing.quantity * existing.unitPrice;
        } else {
            this.cart.push({
                productId: product.id,
                productName: product.name,
                quantity: 1,
                unitPrice: product.price,
                totalPrice: product.price,
                isGenericItem: false
            });
        }

        this.renderCart();
        this.renderProducts(); // Update badges
    },

    removeFromCart(index) {
        this.cart.splice(index, 1);
        this.renderCart();
        this.renderProducts();
    },

    updateQuantity(index, delta) {
        const item = this.cart[index];
        if (!item) return;

        item.quantity += delta;
        if (item.quantity <= 0) {
            this.removeFromCart(index);
            return;
        }

        item.totalPrice = item.quantity * item.unitPrice;
        this.renderCart();
        this.renderProducts();
    },

    clearCart() {
        if (this.cart.length === 0) return;
        this.cart = [];
        this.renderCart();
        this.renderProducts();
    },

    getTotal() {
        return this.cart.reduce((sum, item) => sum + item.totalPrice, 0);
    },

    setPaymentMethod(method) {
        this.paymentMethod = method;
        document.querySelectorAll('.payment-btn').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.method === method);
        });
    },

    renderCart() {
        const container = document.getElementById('cart-items');
        const totalEl = document.getElementById('cart-total-value');
        const payTotalEl = document.getElementById('btn-pay-total');
        const payBtn = document.getElementById('btn-pay');
        const total = this.getTotal();

        totalEl.textContent = formatCurrency(total);
        payTotalEl.textContent = formatCurrency(total);
        payBtn.disabled = this.cart.length === 0;

        if (this.cart.length === 0) {
            container.innerHTML = `
                <div class="cart-empty">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48" opacity="0.3"><path d="M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49A1.003 1.003 0 0 0 20 4H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z"/></svg>
                    <p>Sem artigos</p>
                </div>`;
            return;
        }

        let html = '';
        this.cart.forEach((item, index) => {
            html += `
                <div class="cart-item">
                    <div class="cart-item-info">
                        <div class="cart-item-name">${item.productName}</div>
                        <div class="cart-item-price">${formatCurrency(item.unitPrice)} / un.</div>
                    </div>
                    <div class="cart-item-qty">
                        <button class="qty-btn" onclick="pos.updateQuantity(${index}, -1)">−</button>
                        <span class="qty-value">${item.quantity}</span>
                        <button class="qty-btn" onclick="pos.updateQuantity(${index}, 1)">+</button>
                    </div>
                    <div class="cart-item-total">${formatCurrency(item.totalPrice)}</div>
                    <button class="cart-item-remove" onclick="pos.removeFromCart(${index})">✕</button>
                </div>`;
        });

        container.innerHTML = html;
    },

    // Generic product modal
    showGenericModal() {
        document.getElementById('generic-description').value = '';
        document.getElementById('generic-value').value = '';
        document.getElementById('modal-generic').classList.add('active');
        document.getElementById('generic-description').focus();
    },

    closeGenericModal() {
        document.getElementById('modal-generic').classList.remove('active');
    },

    numpadInput(key) {
        const input = document.getElementById('generic-value');
        let val = input.value;

        if (key === 'del') {
            input.value = val.slice(0, -1);
        } else if (key === '.') {
            if (!val.includes('.')) input.value = val + '.';
        } else {
            // Limit decimal places to 2
            const parts = val.split('.');
            if (parts.length > 1 && parts[1].length >= 2) return;
            input.value = val + key;
        }
    },

    addGenericItem() {
        const desc = document.getElementById('generic-description').value.trim() || 'Artigo Genérico';
        const value = parseFloat(document.getElementById('generic-value').value);

        if (!value || value <= 0) {
            showToast('Introduza um valor válido', 'warning');
            return;
        }

        this.cart.push({
            productId: null,
            productName: desc,
            quantity: 1,
            unitPrice: value,
            totalPrice: value,
            isGenericItem: true
        });

        this.closeGenericModal();
        this.renderCart();
        this.renderProducts();
    },

    // Process payment
    async processPayment() {
        if (this.cart.length === 0) return;

        // Check if session is open
        if (!app.currentSession || !app.currentSession.id) {
            showToast('Abra a caixa antes de processar pagamentos', 'warning');
            app.showOpenSessionModal();
            return;
        }

        try {
            const total = this.getTotal();
            const data = {
                cashSessionId: app.currentSession.id,
                totalAmount: total,
                paymentMethod: this.paymentMethod,
                items: this.cart.map(item => ({
                    productId: item.productId,
                    productName: item.productName,
                    quantity: item.quantity,
                    unitPrice: item.unitPrice,
                    totalPrice: item.totalPrice,
                    isGenericItem: item.isGenericItem
                }))
            };

            await bridge.send('processTransaction', { data });

            // Show success animation
            showPaymentSuccess(total);

            // Update session info
            app.currentSession.totalSales = (app.currentSession.totalSales || 0) + total;
            app.currentSession.totalTransactions = (app.currentSession.totalTransactions || 0) + 1;
            app.updateSessionUI();

            // Clear cart
            this.cart = [];
            this.renderCart();
            this.renderProducts();

        } catch (e) {
            showToast('Erro ao processar pagamento: ' + e.message, 'error');
        }
    }
};
