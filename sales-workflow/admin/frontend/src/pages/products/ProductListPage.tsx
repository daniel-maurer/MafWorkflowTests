import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
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
  const [total, setTotal] = useState(0);

  async function loadData() {
    setLoading(true);
    try {
      const [prodRes, catRes] = await Promise.all([
        adminApi.listProducts({
          search: search || undefined,
          categoryId: selectedCategory || undefined,
          pageSize: 50,
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
  }, [selectedCategory]);

  async function handleDelete(id: string, name: string) {
    if (!window.confirm(`Deseja realmente desativar o produto "${name}"?`)) return;
    try {
      await adminApi.deleteProduct(id);
      loadData();
    } catch (err: any) {
      alert(`Erro: ${err.message}`);
    }
  }

  return (
    <AdminShell
      title="Catálogo de Produtos"
      subtitle={`${total} produto(s) cadastrados com embeddings pgvector`}
      action={
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => navigate('/products/new')}
          style={{ fontSize: 11 }}
        >
          <Icon name="plus" size={14} />
          <span>Cadastrar Produto</span>
        </button>
      }
    >
      {/* Filters Bar */}
      <div
        className="surface-card"
        style={{
          padding: 'var(--space-3) var(--space-4)',
          marginBottom: 'var(--space-4)',
          display: 'flex',
          gap: 'var(--space-3)',
          alignItems: 'center',
        }}
      >
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          <Icon name="search" size={15} />
          <input
            type="text"
            className="input"
            placeholder="Buscar por nome, SKU, marca..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && loadData()}
            style={{ width: '100%', fontSize: 'var(--text-xs)', height: 32 }}
          />
        </div>

        <select
          className="input"
          value={selectedCategory}
          onChange={(e) => setSelectedCategory(e.target.value)}
          style={{ fontSize: 'var(--text-xs)', height: 32, width: 180 }}
        >
          <option value="">Todas as Categorias</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>

        <button type="button" className="btn btn-secondary" onClick={loadData} style={{ fontSize: 11, height: 32 }}>
          Filtrar
        </button>
      </div>

      {/* Products Table */}
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
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
                <td colSpan={8} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Carregando produtos...
                </td>
              </tr>
            ) : products.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Nenhum produto encontrado.
                </td>
              </tr>
            ) : (
              products.map((p) => (
                <tr key={p.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', fontWeight: 600 }}>{p.sku}</td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <div style={{ fontWeight: 600 }}>{p.name}</div>
                    {p.brand && <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{p.brand}</div>}
                  </td>
                  <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                    {p.categoryName || '—'}
                  </td>
                  <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                    {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: p.currency || 'BRL' }).format(p.price)}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <span style={{ color: p.inStock ? 'var(--color-text)' : 'var(--color-danger)' }}>
                      {p.inStock ? `${p.stockQty} un.` : 'Esgotado'}
                    </span>
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <StatusBadge status={p.active} />
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <span
                      style={{
                        fontSize: 10,
                        padding: '2px 6px',
                        borderRadius: 4,
                        background: p.hasEmbedding ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                        color: p.hasEmbedding ? 'var(--color-success)' : 'var(--color-danger)',
                      }}
                    >
                      {p.hasEmbedding ? 'Vector OK' : 'Sem Vector'}
                    </span>
                  </td>
                  <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 'var(--space-2)' }}>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => navigate(`/products/${p.id}`)}
                        style={{ padding: '4px 8px' }}
                        title="Editar"
                      >
                        <Icon name="pen-line" size={13} />
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => handleDelete(p.id, p.name)}
                        style={{ padding: '4px 8px', color: 'var(--color-danger)' }}
                        title="Desativar"
                      >
                        <Icon name="x" size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </AdminShell>
  );
}
