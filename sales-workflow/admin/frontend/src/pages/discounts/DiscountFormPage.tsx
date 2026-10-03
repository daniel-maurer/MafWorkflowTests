import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi } from '@/services/adminApiClient';

export function DiscountFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEditing = Boolean(id);
  const navigate = useNavigate();

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [discountType, setDiscountType] = useState<'percentage' | 'fixed_amount'>('percentage');
  const [discountValue, setDiscountValue] = useState('10');
  const [minOrderValue, setMinOrderValue] = useState('');
  const [maxDiscountAmount, setMaxDiscountAmount] = useState('');
  const [validFrom, setValidFrom] = useState('');
  const [validUntil, setValidUntil] = useState('');
  const [maxUses, setMaxUses] = useState('');
  const [active, setActive] = useState(true);
  const [stackable, setStackable] = useState(false);

  const [paymentPix, setPaymentPix] = useState(true);
  const [paymentCard, setPaymentCard] = useState(true);
  const [paymentBoleto, setPaymentBoleto] = useState(true);

  useEffect(() => {
    if (id) {
      adminApi.getDiscount(id)
        .then((d) => {
          setCode(d.code);
          setName(d.name);
          setDescription(d.description || '');
          setDiscountType(d.discountType);
          setDiscountValue(String(d.discountValue));
          setMinOrderValue(d.minOrderValue ? String(d.minOrderValue) : '');
          setMaxDiscountAmount(d.maxDiscountAmount ? String(d.maxDiscountAmount) : '');
          setValidFrom(d.validFrom ? d.validFrom.slice(0, 16) : '');
          setValidUntil(d.validUntil ? d.validUntil.slice(0, 16) : '');
          setMaxUses(d.maxUses ? String(d.maxUses) : '');
          setActive(d.active);
          setStackable(d.stackable);
          setPaymentPix(d.paymentMethods.includes('pix'));
          setPaymentCard(d.paymentMethods.includes('credit_card'));
          setPaymentBoleto(d.paymentMethods.includes('boleto'));
        })
        .catch((err) => setError(err.message));
    }
  }, [id]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const paymentMethods: string[] = [];
    if (paymentPix) paymentMethods.push('pix');
    if (paymentCard) paymentMethods.push('credit_card');
    if (paymentBoleto) paymentMethods.push('boleto');

    const payload = {
      code: code.trim().toUpperCase(),
      name: name.trim(),
      description: description.trim() || undefined,
      discountType,
      discountValue: parseFloat(discountValue) || 0,
      minOrderValue: minOrderValue ? parseFloat(minOrderValue) : undefined,
      maxDiscountAmount: maxDiscountAmount ? parseFloat(maxDiscountAmount) : undefined,
      validFrom: validFrom ? new Date(validFrom).toISOString() : undefined,
      validUntil: validUntil ? new Date(validUntil).toISOString() : undefined,
      paymentMethods,
      maxUses: maxUses ? parseInt(maxUses, 10) : undefined,
      active,
      stackable,
    };

    try {
      if (isEditing && id) {
        await adminApi.updateDiscount(id, payload);
      } else {
        await adminApi.createDiscount(payload);
      }
      navigate('/discounts');
    } catch (err: any) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <AdminShell
      title={isEditing ? `Editar Cupom: ${code}` : 'Criar Novo Cupom'}
      subtitle="Configure regras de elegibilidade, formas de pagamento aceitas e teto de desconto"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 750 }}>
        {error && (
          <div style={{ padding: 'var(--space-3)', background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', borderRadius: 'var(--radius-md)', marginBottom: 'var(--space-4)', fontSize: 'var(--text-xs)' }}>
            {error}
          </div>
        )}

        <div className="surface-card" style={{ padding: 'var(--space-5)', marginBottom: 'var(--space-5)' }}>
          <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-4)' }}>Regras do Desconto</h3>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Código do Cupom *</label>
              <input
                type="text"
                className="input"
                required
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                placeholder="Ex: PROMO15"
                style={{ width: '100%', fontSize: 'var(--text-xs)', fontFamily: 'monospace' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Nome da Oferta *</label>
              <input
                type="text"
                className="input"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ex: Desconto de Boas-Vindas 15%"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ marginBottom: 'var(--space-4)' }}>
            <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Descrição / Justificativa</label>
            <input
              type="text"
              className="input"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Ex: Válido para primeira compra acima de R$ 200"
              style={{ width: '100%', fontSize: 'var(--text-xs)' }}
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Tipo de Desconto</label>
              <select
                className="input"
                value={discountType}
                onChange={(e) => setDiscountType(e.target.value as any)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              >
                <option value="percentage">Percentual (%)</option>
                <option value="fixed_amount">Valor Fixo (R$)</option>
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Valor do Desconto *</label>
              <input
                type="number"
                step="0.01"
                className="input"
                required
                value={discountValue}
                onChange={(e) => setDiscountValue(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Pedido Mínimo (R$)</label>
              <input
                type="number"
                step="0.01"
                className="input"
                value={minOrderValue}
                onChange={(e) => setMinOrderValue(e.target.value)}
                placeholder="Sem mínimo"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Teto Máximo (R$)</label>
              <input
                type="number"
                step="0.01"
                className="input"
                value={maxDiscountAmount}
                onChange={(e) => setMaxDiscountAmount(e.target.value)}
                placeholder="Sem teto"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Início da Vigência</label>
              <input
                type="datetime-local"
                className="input"
                value={validFrom}
                onChange={(e) => setValidFrom(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Fim da Vigência</label>
              <input
                type="datetime-local"
                className="input"
                value={validUntil}
                onChange={(e) => setValidUntil(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Limite de Usos</label>
              <input
                type="number"
                className="input"
                value={maxUses}
                onChange={(e) => setMaxUses(e.target.value)}
                placeholder="Ilimitado"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 6 }}>Formas de Pagamento Elegíveis</label>
            <div style={{ display: 'flex', gap: 'var(--space-4)' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 'var(--text-xs)', cursor: 'pointer' }}>
                <input type="checkbox" checked={paymentPix} onChange={(e) => setPaymentPix(e.target.checked)} />
                Pix à vista
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 'var(--text-xs)', cursor: 'pointer' }}>
                <input type="checkbox" checked={paymentCard} onChange={(e) => setPaymentCard(e.target.checked)} />
                Cartão de Crédito
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 'var(--text-xs)', cursor: 'pointer' }}>
                <input type="checkbox" checked={paymentBoleto} onChange={(e) => setPaymentBoleto(e.target.checked)} />
                Boleto Bancário
              </label>
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
          <button type="submit" className="btn btn-primary" disabled={saving}>
            <Icon name="check" size={15} />
            <span>{saving ? 'Gravando...' : isEditing ? 'Salvar Alterações' : 'Criar Cupom'}</span>
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => navigate('/discounts')}>
            Cancelar
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
