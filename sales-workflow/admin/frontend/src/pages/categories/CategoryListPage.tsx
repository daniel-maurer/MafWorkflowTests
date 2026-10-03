import { useEffect, useState, type FormEvent } from 'react';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type CategoryItem } from '@/services/adminApiClient';

export function CategoryListPage() {
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingCategory, setEditingCategory] = useState<CategoryItem | null>(null);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [slug, setSlug] = useState('');
  const [active, setActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadData() {
    setLoading(true);
    try {
      const data = await adminApi.listCategories();
      setCategories(data);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  function handleOpenCreate() {
    setEditingCategory(null);
    setName('');
    setDescription('');
    setSlug('');
    setActive(true);
    setError(null);
    setShowModal(true);
  }

  function handleOpenEdit(cat: CategoryItem) {
    setEditingCategory(cat);
    setName(cat.name);
    setDescription(cat.description || '');
    setSlug(cat.slug);
    setActive(cat.active);
    setError(null);
    setShowModal(true);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload = {
      name: name.trim(),
      description: description.trim() || undefined,
      slug: slug.trim() || undefined,
      active,
    };

    try {
      if (editingCategory) {
        await adminApi.updateCategory(editingCategory.id, payload);
      } else {
        await adminApi.createCategory(payload);
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string, catName: string) {
    if (!window.confirm(`Deseja desativar a categoria "${catName}"?`)) return;
    try {
      await adminApi.deleteCategory(id);
      loadData();
    } catch (err: any) {
      alert(err.message);
    }
  }

  return (
    <AdminShell
      title="Categorias de Produtos"
      subtitle="Organização estruturada do catálogo para filtragem e classificação de intenções"
      action={
        <button type="button" className="btn btn-primary" onClick={handleOpenCreate} style={{ fontSize: 11 }}>
          <Icon name="plus" size={14} />
          <span>Nova Categoria</span>
        </button>
      }
    >
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
              <th style={{ padding: 'var(--space-3)' }}>Nome</th>
              <th style={{ padding: 'var(--space-3)' }}>Slug</th>
              <th style={{ padding: 'var(--space-3)' }}>Descrição</th>
              <th style={{ padding: 'var(--space-3)' }}>Status</th>
              <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Carregando categorias...
                </td>
              </tr>
            ) : categories.length === 0 ? (
              <tr>
                <td colSpan={5} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Nenhuma categoria cadastrada.
                </td>
              </tr>
            ) : (
              categories.map((c) => (
                <tr key={c.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>{c.name}</td>
                  <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', color: 'var(--color-text-muted)' }}>{c.slug}</td>
                  <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>{c.description || '—'}</td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <StatusBadge status={c.active} />
                  </td>
                  <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 'var(--space-2)' }}>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => handleOpenEdit(c)}
                        style={{ padding: '4px 8px' }}
                        title="Editar"
                      >
                        <Icon name="pen-line" size={13} />
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => handleDelete(c.id, c.name)}
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

      {/* Modal de Criação / Edição */}
      {showModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'grid',
            placeItems: 'center',
            zIndex: 1000,
          }}
        >
          <div className="surface-card" style={{ width: 450, padding: 'var(--space-5)' }}>
            <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-4)' }}>
              {editingCategory ? 'Editar Categoria' : 'Nova Categoria'}
            </h3>

            {error && (
              <div style={{ padding: 'var(--space-2)', background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', borderRadius: 'var(--radius-md)', marginBottom: 'var(--space-3)', fontSize: 11 }}>
                {error}
              </div>
            )}

            <form onSubmit={handleSubmit}>
              <div style={{ marginBottom: 'var(--space-3)' }}>
                <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Nome *</label>
                <input
                  type="text"
                  className="input"
                  required
                  value={name}
                  onChange={(e) => {
                    setName(e.target.value);
                    if (!editingCategory) {
                      setSlug(e.target.value.toLowerCase().replace(/\s+/g, '-'));
                    }
                  }}
                  style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                />
              </div>

              <div style={{ marginBottom: 'var(--space-3)' }}>
                <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Slug</label>
                <input
                  type="text"
                  className="input"
                  value={slug}
                  onChange={(e) => setSlug(e.target.value)}
                  style={{ width: '100%', fontSize: 'var(--text-xs)', fontFamily: 'monospace' }}
                />
              </div>

              <div style={{ marginBottom: 'var(--space-4)' }}>
                <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Descrição</label>
                <textarea
                  className="input"
                  rows={3}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-2)' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)} disabled={saving}>
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? 'Salvando...' : 'Salvar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AdminShell>
  );
}
