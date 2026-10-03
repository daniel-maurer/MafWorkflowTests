import { useEffect, useState, type FormEvent } from 'react';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type PaymentConditionItem } from '@/services/adminApiClient';

export function PaymentConditionsPage() {
  const [conditions, setConditions] = useState<PaymentConditionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<PaymentConditionItem | null>(null);

  const [name, setName] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<'pix' | 'credit_card' | 'debit_card' | 'boleto'>('pix');
  const [maxInstallments, setMaxInstallments] = useState('1');
  const [interestFree, setInterestFree] = useState(true);
  const [interestRate, setInterestRate] = useState('0');
  const [additionalDiscount, setAdditionalDiscount] = useState('0');
  const [active, setActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadData() {
    setLoading(true);
    try {
      const data = await adminApi.listPaymentConditions();
      setConditions(data);
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
    setEditing(null);
    setName('');
    setPaymentMethod('pix');
    setMaxInstallments('1');
    setInterestFree(true);
    setInterestRate('0');
    setAdditionalDiscount('5');
    setActive(true);
    setError(null);
    setShowModal(true);
  }

  function handleOpenEdit(item: PaymentConditionItem) {
    setEditing(item);
    setName(item.name);
    setPaymentMethod(item.paymentMethod);
    setMaxInstallments(String(item.maxInstallments));
    setInterestFree(item.interestFree);
    setInterestRate(String(item.interestRate));
    setAdditionalDiscount(String(item.additionalDiscount));
    setActive(item.active);
    setError(null);
    setShowModal(true);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload = {
      name: name.trim(),
      paymentMethod,
      maxInstallments: parseInt(maxInstallments, 10) || 1,
      interestFree,
      interestRate: parseFloat(interestRate) || 0,
      additionalDiscount: parseFloat(additionalDiscount) || 0,
      active,
    };

    try {
      if (editing) {
        await adminApi.updatePaymentCondition(editing.id, payload);
      } else {
        await adminApi.createPaymentCondition(payload);
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string, condName: string) {
    if (!window.confirm(`Deseja desativar a condição "${condName}"?`)) return;
    try {
      await adminApi.deletePaymentCondition(id);
      loadData();
    } catch (err: any) {
      alert(err.message);
    }
  }

  return (
    <AdminShell
      title="Condições de Pagamento"
      subtitle="Regras comerciais dinâmicas consumidas pelo QuoteAgent na emissão de orçamentos"
      action={
        <button type="button" className="btn btn-primary" onClick={handleOpenCreate} style={{ fontSize: 11 }}>
          <Icon name="plus" size={14} />
          <span>Nova Condição</span>
        </button>
      }
    >
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
              <th style={{ padding: 'var(--space-3)' }}>Nome da Regra</th>
              <th style={{ padding: 'var(--space-3)' }}>Método</th>
              <th style={{ padding: 'var(--space-3)' }}>Parcelamento</th>
              <th style={{ padding: 'var(--space-3)' }}>Juros</th>
              <th style={{ padding: 'var(--space-3)' }}>Desconto Extra</th>
              <th style={{ padding: 'var(--space-3)' }}>Status</th>
              <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Carregando condições...
                </td>
              </tr>
            ) : conditions.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Nenhuma condição configurada.
                </td>
              </tr>
            ) : (
              conditions.map((item) => (
                <tr key={item.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>{item.name}</td>
                  <td style={{ padding: 'var(--space-3)', textTransform: 'uppercase', fontFamily: 'monospace' }}>
                    {item.paymentMethod}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    {item.maxInstallments === 1 ? 'À vista' : `Até ${item.maxInstallments}x`}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    {item.interestFree ? 'Sem juros' : `${item.interestRate}% a.m.`}
                  </td>
                  <td style={{ padding: 'var(--space-3)', fontWeight: item.additionalDiscount > 0 ? 600 : 400, color: item.additionalDiscount > 0 ? 'var(--color-success)' : 'inherit' }}>
                    {item.additionalDiscount > 0 ? `${item.additionalDiscount}% OFF` : '—'}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <StatusBadge status={item.active} />
                  </td>
                  <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 'var(--space-2)' }}>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => handleOpenEdit(item)}
                        style={{ padding: '4px 8px' }}
                        title="Editar"
                      >
                        <Icon name="pen-line" size={13} />
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => handleDelete(item.id, item.name)}
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

      {/* Modal */}
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
          <div className="surface-card" style={{ width: 480, padding: 'var(--space-5)' }}>
            <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-4)' }}>
              {editing ? 'Editar Condição' : 'Nova Condição de Pagamento'}
            </h3>

            {error && (
              <div style={{ padding: 'var(--space-2)', background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', borderRadius: 'var(--radius-md)', marginBottom: 'var(--space-3)', fontSize: 11 }}>
                {error}
              </div>
            )}

            <form onSubmit={handleSubmit}>
              <div style={{ marginBottom: 'var(--space-3)' }}>
                <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Nome da Regra *</label>
                <input
                  type="text"
                  className="input"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Ex: Pix com 5% de desconto"
                  style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Método</label>
                  <select
                    className="input"
                    value={paymentMethod}
                    onChange={(e) => setPaymentMethod(e.target.value as any)}
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  >
                    <option value="pix">Pix</option>
                    <option value="credit_card">Cartão de Crédito</option>
                    <option value="debit_card">Cartão de Débito</option>
                    <option value="boleto">Boleto Bancário</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Máximo de Parcelas</label>
                  <input
                    type="number"
                    min="1"
                    max="36"
                    className="input"
                    value={maxInstallments}
                    onChange={(e) => setMaxInstallments(e.target.value)}
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)', marginBottom: 'var(--space-4)' }}>
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Desconto Extra (%)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={additionalDiscount}
                    onChange={(e) => setAdditionalDiscount(e.target.value)}
                    placeholder="Ex: 5 para 5%"
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Taxa de Juros (% a.m.)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={interestRate}
                    onChange={(e) => {
                      setInterestRate(e.target.value);
                      setInterestFree(parseFloat(e.target.value) === 0);
                    }}
                    placeholder="0 para sem juros"
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: 'var(--space-4)' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 'var(--text-xs)', cursor: 'pointer' }}>
                  <input type="checkbox" checked={interestFree} onChange={(e) => setInterestFree(e.target.checked)} />
                  Sem juros
                </label>
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
