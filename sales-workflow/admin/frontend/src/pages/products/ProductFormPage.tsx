import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi, type CategoryItem } from '@/services/adminApiClient';

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEditing = Boolean(id);
  const navigate = useNavigate();

  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [sku, setSku] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('0');
  const [currency, setCurrency] = useState('BRL');
  const [inStock, setInStock] = useState(true);
  const [stockQty, setStockQty] = useState('0');
  const [color, setColor] = useState('');
  const [size, setSize] = useState('');
  const [brand, setBrand] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [tagsStr, setTagsStr] = useState('');
  const [compatibleSkusStr, setCompatibleSkusStr] = useState('');
  const [imageUrl, setImageUrl] = useState('');
  const [active, setActive] = useState(true);

  useEffect(() => {
    adminApi.listCategories(true).then(setCategories).catch(console.error);

    if (id) {
      setLoading(true);
      adminApi.getProduct(id)
        .then((p) => {
          setSku(p.sku);
          setName(p.name);
          setDescription(p.description);
          setPrice(String(p.price));
          setCurrency(p.currency);
          setInStock(p.inStock);
          setStockQty(String(p.stockQty));
          setColor(p.color || '');
          setSize(p.size || '');
          setBrand(p.brand || '');
          setCategoryId(p.categoryId || '');
          setTagsStr(p.tags?.join(', ') || '');
          setCompatibleSkusStr(p.compatibleSkus?.join(', ') || '');
          setImageUrl(p.imageUrl || '');
          setActive(p.active);
        })
        .catch((err) => setError(err.message))
        .finally(() => setLoading(false));
    }
  }, [id]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const tags = tagsStr
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean);
    const compatibleSkus = compatibleSkusStr
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);

    const payload = {
      sku: sku.trim(),
      name: name.trim(),
      description: description.trim(),
      price: parseFloat(price) || 0,
      currency,
      inStock,
      stockQty: parseInt(stockQty, 10) || 0,
      color: color.trim() || undefined,
      size: size.trim() || undefined,
      brand: brand.trim() || undefined,
      categoryId: categoryId || undefined,
      tags,
      compatibleSkus,
      imageUrl: imageUrl.trim() || undefined,
      active,
    };

    try {
      if (isEditing && id) {
        await adminApi.updateProduct(id, payload);
      } else {
        await adminApi.createProduct(payload);
      }
      navigate('/products');
    } catch (err: any) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <AdminShell
      title={isEditing ? `Editar Produto: ${name || sku}` : 'Cadastrar Novo Produto'}
      subtitle="Os dados e descrições são automaticamente vetorizados (pgvector) para busca no workflow"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 800 }}>
        {error && (
          <div style={{ padding: 'var(--space-3)', background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', borderRadius: 'var(--radius-md)', marginBottom: 'var(--space-4)', fontSize: 'var(--text-xs)' }}>
            {error}
          </div>
        )}

        <div className="surface-card" style={{ padding: 'var(--space-5)', marginBottom: 'var(--space-5)' }}>
          <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-4)' }}>Dados Principais</h3>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>SKU *</label>
              <input
                type="text"
                className="input"
                required
                value={sku}
                onChange={(e) => setSku(e.target.value)}
                placeholder="Ex: CAM-POLO-AZ-M"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Nome do Produto *</label>
              <input
                type="text"
                className="input"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ex: Camiseta Polo Clássica"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ marginBottom: 'var(--space-4)' }}>
            <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Descrição Comercial (usada na busca RAG) *</label>
            <textarea
              className="input"
              rows={4}
              required
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Descreva detalhes, tecido, especificações, compatibilidades..."
              style={{ width: '100%', fontSize: 'var(--text-xs)', resize: 'vertical' }}
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Preço (R$) *</label>
              <input
                type="number"
                step="0.01"
                className="input"
                required
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Quantidade em Estoque</label>
              <input
                type="number"
                className="input"
                value={stockQty}
                onChange={(e) => {
                  setStockQty(e.target.value);
                  setInStock(parseInt(e.target.value, 10) > 0);
                }}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Categoria</label>
              <select
                className="input"
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              >
                <option value="">Selecione...</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Marca</label>
              <input
                type="text"
                className="input"
                value={brand}
                onChange={(e) => setBrand(e.target.value)}
                placeholder="Ex: Nike, Lenovo"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 2fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Cor</label>
              <input
                type="text"
                className="input"
                value={color}
                onChange={(e) => setColor(e.target.value)}
                placeholder="Ex: Azul Marinho"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Tamanho</label>
              <input
                type="text"
                className="input"
                value={size}
                onChange={(e) => setSize(e.target.value)}
                placeholder="Ex: M, 42, 15.6 polegadas"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>URL da Imagem</label>
              <input
                type="text"
                className="input"
                value={imageUrl}
                onChange={(e) => setImageUrl(e.target.value)}
                placeholder="Ex: /images/products/CAM-POLO.jpg ou URL externa"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Tags de Busca (separadas por vírgula)</label>
              <input
                type="text"
                className="input"
                value={tagsStr}
                onChange={(e) => setTagsStr(e.target.value)}
                placeholder="algodão, casual, slim, verão"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>SKUs Compatíveis / Cross-Sell (separados por vírgula)</label>
              <input
                type="text"
                className="input"
                value={compatibleSkusStr}
                onChange={(e) => setCompatibleSkusStr(e.target.value)}
                placeholder="MOUS-LOG-WL, FONE-JBL-T510"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>
        </div>

        {/* Buttons */}
        <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
          <button type="submit" className="btn btn-primary" disabled={saving}>
            <Icon name="check" size={15} />
            <span>{saving ? 'Gravando e gerando embedding...' : isEditing ? 'Salvar Alterações' : 'Cadastrar Produto'}</span>
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => navigate('/products')}>
            Cancelar
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
