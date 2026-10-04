import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Icon, Input, Select, Textarea, Switch, Card } from '@maf/shared-admin-app';
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
  const [stockQty, setStockQty] = useState('3');
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
      adminApi
        .getProduct(id)
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

  const categoryOptions = [
    { value: '', label: 'Selecione uma categoria...' },
    ...categories.map((c) => ({ value: c.id, label: c.name })),
  ];

  if (loading) {
    return (
      <AdminShell title="Carregando..." subtitle="Buscando detalhes do produto">
        <div style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
          Carregando dados do produto...
        </div>
      </AdminShell>
    );
  }

  return (
    <AdminShell
      title={isEditing ? `Editar Produto: ${name || sku}` : 'Cadastrar Novo Produto'}
      subtitle="Os dados e descrições são automaticamente vetorizados (pgvector) para busca no workflow"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 840, display: 'flex', flexDirection: 'column', gap: 'var(--space-5)' }}>
        {error && (
          <div
            style={{
              padding: 'var(--space-3) var(--space-4)',
              background: 'var(--color-error-bg)',
              color: 'var(--color-error)',
              borderRadius: 'var(--radius-md)',
              border: '1px solid rgba(248, 113, 113, 0.3)',
              fontSize: 'var(--text-xs)',
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
            }}
          >
            <Icon name="alert-circle" size={16} />
            <span>{error}</span>
          </div>
        )}

        {/* 1. Identificação Básica */}
        <Card
          title="Identificação & Nome"
          subtitle="Identificador único (SKU) e denominação comercial do item"
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <Input
              label="SKU"
              required
              value={sku}
              onChange={(e) => setSku(e.target.value)}
              placeholder="Ex: CAM-POLO-AZ-M"
              leftIcon="tag"
              helperText="Código alfanumérico único"
            />
            <Input
              label="Nome do Produto"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ex: Camiseta Polo Linho Misto"
              leftIcon="shopping-bag"
            />
          </div>

          <Textarea
            label="Descrição Comercial (usada na busca vetorial RAG)"
            required
            rows={4}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Descreva detalhes, composição, caimento, tecidos e características..."
            helperText="Este texto é indexado pelo modelo de embeddings para busca semântica pelo agente"
          />
        </Card>

        {/* 2. Preço, Categoria & Estoque */}
        <Card
          title="Preço & Disponibilidade de Estoque"
          subtitle="Valores, quantitativo e classificação"
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <Input
              label="Preço de Venda (R$)"
              type="number"
              step="0.01"
              required
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              leftIcon="dollar-sign"
            />

            <Input
              label="Quantidade em Estoque"
              type="number"
              required
              value={stockQty}
              onChange={(e) => {
                setStockQty(e.target.value);
                setInStock(parseInt(e.target.value, 10) > 0);
              }}
              leftIcon="package"
            />

            <Select
              label="Categoria"
              options={categoryOptions}
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              leftIcon="layers"
            />
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-6)', padding: 'var(--space-2) 0' }}>
            <Switch
              checked={inStock}
              onChange={(checked) => setInStock(checked)}
              label="Disponível para Venda"
              description="Quando ativo, o agente pode ofertar este produto"
            />

            <Switch
              checked={active}
              onChange={(checked) => setActive(checked)}
              label="Cadastro Ativo"
              description="Desative para ocultar este item do sistema"
            />
          </div>
        </Card>

        {/* 3. Características & Variações */}
        <Card
          title="Características & Variações"
          subtitle="Atributos de cor, tamanho e fabricante"
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <Input
              label="Marca / Fabricante"
              value={brand}
              onChange={(e) => setBrand(e.target.value)}
              placeholder="Ex: Nike, Aramis, Dudalina"
              leftIcon="award"
            />

            <Input
              label="Cor Predominante"
              value={color}
              onChange={(e) => setColor(e.target.value)}
              placeholder="Ex: Azul Bebê, Terracota"
              leftIcon="palette"
            />

            <Input
              label="Tamanho / Grade"
              value={size}
              onChange={(e) => setSize(e.target.value)}
              placeholder="Ex: P, M, G, GG, 42"
              leftIcon="maximize"
            />
          </div>

          <div style={{ marginBottom: 'var(--space-4)' }}>
            <Input
              label="URL da Imagem / SVG"
              value={imageUrl}
              onChange={(e) => setImageUrl(e.target.value)}
              placeholder="Ex: /images/products/CAM-NIK-AZ-M-139.svg ou URL pública"
              leftIcon="image"
              helperText="Caminho relativo para imagem local ou URL HTTP"
            />
            {imageUrl && (
              <div style={{ marginTop: '8px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>Prévia:</span>
                <img
                  src={imageUrl}
                  alt="Prévia do produto"
                  style={{
                    width: 48,
                    height: 48,
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--color-border)',
                    backgroundColor: 'var(--color-surface-2)',
                    objectFit: 'cover',
                  }}
                  onError={(e) => {
                    (e.target as HTMLElement).style.display = 'none';
                  }}
                />
              </div>
            )}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Input
              label="Tags de Busca"
              value={tagsStr}
              onChange={(e) => setTagsStr(e.target.value)}
              placeholder="esportiva, dryfit, running, respirável"
              leftIcon="hash"
              helperText="Termos separados por vírgula para refinar o RAG"
            />

            <Input
              label="SKUs Compatíveis (Cross-Sell / Combos)"
              value={compatibleSkusStr}
              onChange={(e) => setCompatibleSkusStr(e.target.value)}
              placeholder="CALC-NIK-AZ-P-121, TEN-NIK-AZ-41"
              leftIcon="link"
              helperText="Produtos que combinam para sugestão de kits"
            />
          </div>
        </Card>

        {/* Barra de Ações */}
        <div style={{ display: 'flex', gap: 'var(--space-3)', justifyContent: 'flex-end', paddingTop: 'var(--space-2)' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => navigate('/products')}
            style={{ padding: '8px 20px', fontSize: 12 }}
          >
            Cancelar
          </button>

          <button
            type="submit"
            className="btn btn-primary"
            disabled={saving}
            style={{ padding: '8px 24px', fontSize: 12, gap: '8px' }}
          >
            <Icon name="check" size={16} />
            <span>{saving ? 'Gravando e gerando embedding...' : isEditing ? 'Salvar Alterações' : 'Cadastrar Produto'}</span>
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
