import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon, Input, Select, Pagination } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type ProductItem, type CategoryItem } from '@/services/adminApiClient';

export function ProductListPage() {
  const navigate = useNavigate();
  const [products, setProducts] = useState<ProductItem[]>([]);
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [total, setTotal] = useState(0);

  async function loadData() {
    setLoading(true);
    try {
      const [prodRes, catRes] = await Promise.all([
        adminApi.listProducts({
          search: search.trim() || undefined,
          categoryId: selectedCategory || undefined,
          page,
          pageSize,
        }),
        adminApi.listCategories(),
      ]);
      setProducts(prodRes.items);
      setTotal(prodRes.total);
      setCategories(catRes);
    } catch (err) {
      console.error('Erro ao carregar produtos:', err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, [page, pageSize, selectedCategory]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    loadData();
  }

  async function handleDelete(id: string, name: string) {
    if (!window.confirm(`Deseja realmente desativar o produto "${name}"?`)) return;
    try {
      await adminApi.deleteProduct(id);
      loadData();
    } catch (err: any) {
      alert(`Erro: ${err.message}`);
    }
  }

  const categoryOptions = [
    { value: '', label: 'Todas as Categorias' },
    ...categories.map((c) => ({ value: c.id, label: c.name })),
  ];

  return (
    <AdminShell
      title="Catálogo de Produtos"
      subtitle={`${total} produto(s) cadastrados com embeddings pgvector`}
      action={
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => navigate('/products/new')}
          style={{ fontSize: 12, padding: '8px 16px', gap: '8px' }}
        >
          <Icon name="plus" size={15} />
          <span>Cadastrar Produto</span>
        </button>
      }
    >
      {/* Barra de Filtros Moderna */}
      <form
        onSubmit={handleSearchSubmit}
        className="surface-card"
        style={{
          padding: 'var(--space-3) var(--space-4)',
          marginBottom: 'var(--space-4)',
          display: 'grid',
          gridTemplateColumns: '1fr 220px auto',
          gap: 'var(--space-3)',
          alignItems: 'center',
        }}
      >
        <Input
          placeholder="Buscar por nome, SKU, marca..."
          leftIcon="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />

        <Select
          options={categoryOptions}
          value={selectedCategory}
          onChange={(e) => {
            setSelectedCategory(e.target.value);
            setPage(1);
          }}
        />

        <button
          type="submit"
          className="btn btn-secondary"
          style={{ height: '36px', padding: '0 16px', gap: '6px' }}
        >
          <Icon name="search" size={14} />
          <span>Buscar</span>
        </button>
      </form>

      {/* Grid de Produtos */}
      <div className="surface-card" style={{ overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
            <thead>
              <tr
                style={{
                  background: 'var(--color-surface-offset)',
                  borderBottom: '1px solid var(--color-border)',
                  textAlign: 'left',
                }}
              >
                <th style={{ padding: 'var(--space-3)' }}>SKU</th>
                <th style={{ padding: 'var(--space-3)' }}>Produto</th>
                <th style={{ padding: 'var(--space-3)' }}>Categoria</th>
                <th style={{ padding: 'var(--space-3)' }}>Preço</th>
                <th style={{ padding: 'var(--space-3)' }}>Estoque</th>
                <th style={{ padding: 'var(--space-3)' }}>Status</th>
                <th style={{ padding: 'var(--space-3)' }}>RAG</th>
                <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td
                    colSpan={8}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Carregando produtos...
                  </td>
                </tr>
              ) : products.length === 0 ? (
                <tr>
                  <td
                    colSpan={8}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Nenhum produto encontrado para estes filtros.
                  </td>
                </tr>
              ) : (
                products.map((p) => (
                  <tr
                    key={p.id}
                    style={{
                      borderBottom: '1px solid var(--color-border)',
                      transition: 'background-color var(--transition)',
                    }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = 'var(--color-surface-offset)';
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = 'transparent';
                    }}
                  >
                    <td
                      style={{
                        padding: 'var(--space-3)',
                        fontFamily: 'monospace',
                        fontWeight: 600,
                        color: 'var(--color-primary)',
                      }}
                    >
                      {p.sku}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                        {p.imageUrl ? (
                          <img
                            src={p.imageUrl}
                            alt={p.name}
                            style={{
                              width: 32,
                              height: 32,
                              borderRadius: 'var(--radius-sm)',
                              objectFit: 'cover',
                              backgroundColor: 'var(--color-surface-2)',
                              border: '1px solid var(--color-border)',
                            }}
                            onError={(e) => {
                              (e.target as HTMLElement).style.display = 'none';
                            }}
                          />
                        ) : null}
                        <div>
                          <div style={{ fontWeight: 600, color: 'var(--color-text)' }}>{p.name}</div>
                          {p.brand && <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{p.brand}</div>}
                        </div>
                      </div>
                    </td>
                    <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                      {p.categoryName || '—'}
                    </td>
                    <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                      {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: p.currency || 'BRL' }).format(p.price)}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span
                        style={{
                          fontWeight: 500,
                          color: p.inStock && p.stockQty > 0 ? 'var(--color-success)' : 'var(--color-error)',
                        }}
                      >
                        {p.inStock && p.stockQty > 0 ? `${p.stockQty} un.` : 'Esgotado'}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={p.active} />
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span
                        style={{
                          fontSize: 10,
                          fontWeight: 600,
                          padding: '2px 8px',
                          borderRadius: 4,
                          background: p.hasEmbedding ? 'var(--color-success-bg)' : 'var(--color-error-bg)',
                          color: p.hasEmbedding ? 'var(--color-success)' : 'var(--color-error)',
                          border: `1px solid ${p.hasEmbedding ? 'rgba(74, 222, 128, 0.2)' : 'rgba(248, 113, 113, 0.2)'}`,
                        }}
                      >
                        {p.hasEmbedding ? 'Vector OK' : 'Sem Vector'}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => navigate(`/products/${p.id}`)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={14} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(p.id, p.name)}
                          style={{ padding: '4px 8px', color: 'var(--color-error)' }}
                          title="Desativar"
                        >
                          <Icon name="x" size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Paginação Integrada */}
        <Pagination
          currentPage={page}
          totalItems={total}
          pageSize={pageSize}
          onPageChange={(newPage) => setPage(newPage)}
          onPageSizeChange={(newSize) => {
            setPageSize(newSize);
            setPage(1);
          }}
          pageSizeOptions={[15, 30, 50, 100]}
        />
      </div>
    </AdminShell>
  );
}
