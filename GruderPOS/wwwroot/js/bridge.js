// ===== Bridge: JS ↔ C# Communication =====
const bridge = {
    _requestId: 0,
    _callbacks: {},

    async send(action, data = {}) {
        return new Promise((resolve, reject) => {
            const requestId = `req_${++this._requestId}`;
            this._callbacks[requestId] = { resolve, reject };

            const message = { action, requestId, ...data };

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(message);
            } else {
                // Fallback for development/testing
                console.log('Bridge message:', message);
                setTimeout(() => {
                    this._handleFallback(action, data, requestId);
                }, 100);
            }

            // Timeout after 10 seconds
            setTimeout(() => {
                if (this._callbacks[requestId]) {
                    delete this._callbacks[requestId];
                    reject(new Error('Request timeout'));
                }
            }, 10000);
        });
    },

    // Fallback mock data for development without C# backend
    _handleFallback(action, data, requestId) {
        const mockData = {
            getCategories: [
                { id: 1, name: 'Bebidas', sortOrder: 1, isActive: true },
                { id: 2, name: 'Comida', sortOrder: 2, isActive: true },
                { id: 3, name: 'Doces', sortOrder: 3, isActive: true },
                { id: 4, name: 'Outros', sortOrder: 4, isActive: true }
            ],
            getAllCategories: [
                { id: 1, name: 'Bebidas', sortOrder: 1, isActive: true },
                { id: 2, name: 'Comida', sortOrder: 2, isActive: true },
                { id: 3, name: 'Doces', sortOrder: 3, isActive: true },
                { id: 4, name: 'Outros', sortOrder: 4, isActive: true }
            ],
            getAllProducts: [
                { id: 1, categoryId: 1, name: 'Cerveja', price: 1.50, isGeneric: false, isActive: true, sortOrder: 1 },
                { id: 2, categoryId: 1, name: 'Água', price: 0.75, isGeneric: false, isActive: true, sortOrder: 2 },
                { id: 3, categoryId: 1, name: 'Sumo', price: 1.00, isGeneric: false, isActive: true, sortOrder: 3 },
                { id: 4, categoryId: 1, name: 'Refrigerante', price: 1.00, isGeneric: false, isActive: true, sortOrder: 4 },
                { id: 5, categoryId: 1, name: 'Sangria', price: 2.00, isGeneric: false, isActive: true, sortOrder: 5 },
                { id: 6, categoryId: 1, name: 'Vinho', price: 1.50, isGeneric: false, isActive: true, sortOrder: 6 },
                { id: 7, categoryId: 2, name: 'Bifana', price: 3.00, isGeneric: false, isActive: true, sortOrder: 1 },
                { id: 8, categoryId: 2, name: 'Prego', price: 3.50, isGeneric: false, isActive: true, sortOrder: 2 },
                { id: 9, categoryId: 2, name: 'Francesinha', price: 5.00, isGeneric: false, isActive: true, sortOrder: 3 },
                { id: 10, categoryId: 2, name: 'Batatas Fritas', price: 2.00, isGeneric: false, isActive: true, sortOrder: 4 },
                { id: 11, categoryId: 2, name: 'Courato', price: 3.00, isGeneric: false, isActive: true, sortOrder: 5 },
                { id: 12, categoryId: 3, name: 'Bolo', price: 1.50, isGeneric: false, isActive: true, sortOrder: 1 },
                { id: 13, categoryId: 3, name: 'Farturas', price: 2.00, isGeneric: false, isActive: true, sortOrder: 2 },
                { id: 14, categoryId: 3, name: 'Gelado', price: 1.50, isGeneric: false, isActive: true, sortOrder: 3 },
                { id: 15, categoryId: 4, name: 'Artigo Genérico', price: 0.00, isGeneric: true, isActive: true, sortOrder: 1 }
            ],
            getCurrentSession: {},
            getSettings: {
                EventName: 'Festa GRUDER 2026', SerialPort: 'COM3', BaudRate: '9600', PrinterEnabled: 'true',
                PrintMode: 'Complete', PrintCopies: '1',
                HeaderEnabled: 'true', HeaderLine1: 'GRUDER', HeaderLine2: 'GRUPO DESPORTIVO DA',
                HeaderLine3: 'RIBEIRA DO FARRIO', HeaderLine4: 'Fundado em 1977',
                BodyEnabled: 'true', BodyTitle: '', BodyLine1: '', BodyLine2: '',
                ShowDate: 'true', ShowSession: 'true', ShowReceiptNumber: 'true',
                ShowTicketNumber: 'true', ShowGridHeader: 'true',
                ShowPaymentMethod: 'true', ShowTotals: 'true',
                FooterEnabled: 'true', FooterLine1: 'Obrigado pela preferencia!', FooterLine2: 'GRUDER - 1977'
            },
            getCashSessions: [{ session: { id: 1, openedAt: '2026-01-01 09:00:00', closedAt: '2026-01-01 18:00:00', openingBalance: 50, closingBalance: 200, totalSales: 150, totalTransactions: 3, status: 'Closed', notes: null }, paymentBreakdown: [{ method: 'Cash', total: 100, count: 2 }, { method: 'Card', total: 50, count: 1 }] }],
            getSerialPorts: { ports: ['COM1', 'COM3', 'COM4'], currentPort: 'COM3', baudRate: 9600 }
        };

        window.bridgeCallback(requestId, {
            success: true,
            data: mockData[action] || {}
        });
    }
};

// Global callback function called by C#
window.bridgeCallback = function(requestId, response) {
    const cb = bridge._callbacks[requestId];
    if (cb) {
        delete bridge._callbacks[requestId];
        if (response.success) {
            cb.resolve(response.data);
        } else {
            cb.reject(new Error(response.error || 'Unknown error'));
        }
    }
};
