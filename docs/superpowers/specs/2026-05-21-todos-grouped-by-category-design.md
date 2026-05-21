# Design: Agrupar cards por categoria no separador Todos

**Data:** 2026-05-21  
**Âmbito:** `pos.js`, `styles.css`  
**Estado:** Aprovado

---

## Objetivo

No separador "Todos" da grelha de produtos do POS, os cards devem ser apresentados agrupados por categoria, com um header visual por grupo. Os tabs de categorias, a navegação e os filtros existentes mantêm-se sem alterações.

---

## Comportamento atual

- `pos.selectedCategory === null` → tab "Todos" ativo → `renderProducts()` mostra todos os produtos em grelha plana sem separação.
- `pos.selectedCategory !== null` → tab de categoria específica → `renderProducts()` filtra e mostra lista plana da categoria selecionada.

---

## Comportamento desejado

- **Tab "Todos" (`selectedCategory === null`):** produtos agrupados por categoria, com um header de grupo antes de cada conjunto de cards.
- **Tab de categoria específica (`selectedCategory !== null`):** comportamento atual inalterado — lista plana.
- Tabs, `selectCategory()`, `renderCategoryTabs()`, cart, badges — todos inalterados.

---

## Abordagem escolhida

**Abordagem A — Modificar `renderProducts()` com deteção de modo.**

Adicionar um branch em `renderProducts()` que deteta `selectedCategory === null` e produz HTML agrupado. O caso contrário mantém o comportamento atual. Para evitar duplicação do HTML do card, extrai-se um método privado `_productCardHtml(product)`.

---

## Alterações

### 1. `GruderPOS/wwwroot/js/pos.js`

**Extrair método:** `_productCardHtml(product) → string`  
Encapsula o HTML atual do card (badge de quantidade, nome, preço, onclick para addToCart ou showGenericModal). Chamado por ambos os caminhos de renderização.

**Modificar:** `renderProducts()`

```
if (selectedCategory === null):
    para cada categoria (por ordem de pos.categories):
        produtos da categoria = products.filter(categoryId === cat.id)
        se sem produtos → saltar
        emitir <div class="product-group-header">{cat.name}</div>
        para cada produto → emitir _productCardHtml(produto)
else:
    comportamento atual (filtrar por selectedCategory, lista plana)
```

Empty state: mantido — se não houver nenhum produto em nenhuma categoria, apresentar o estado vazio existente.

### 2. `GruderPOS/wwwroot/css/styles.css`

Adicionar classe `.product-group-header`:

```css
.product-group-header {
    grid-column: 1 / -1;
    font-size: 12px;
    font-weight: 700;
    color: var(--text-light);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    padding: 12px 4px 6px;
    border-bottom: 1px solid var(--border);
}
```

`grid-column: 1 / -1` garante que o header ocupa toda a largura do grid independentemente do número de colunas (que é responsivo via `auto-fill`).

---

## Ficheiros não alterados

- `index.html`
- `app.js`
- `history.js`
- `settings.js`
- Qualquer ficheiro C# de backend

---

## Critério de aceitação

1. Abrir o separador "Todos" → cards aparecem agrupados com headers de categoria visíveis.
2. Clicar num tab de categoria → lista plana da categoria (comportamento atual).
3. Clicar de volta em "Todos" → agrupamento restaurado.
4. Adicionar produto ao carrinho a partir do tab "Todos" → badge atualizado corretamente.
5. Produto genérico no tab "Todos" → abre modal normalmente.
