import { useEffect, useState, type FormEvent } from 'react';
import { Icon, Input, Select, Switch, Modal } from '@maf/shared-admin-app';
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
      setError(err.message || 'Erro ao salvar condição de pagamento');
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
        <button type="button" className="btn btn-primary" onClick={handleOpenCreate} style={{ fontSize: 11, gap: 6 }}>
          <Icon name="plus" size={14} />
          <span>Nova Condição</span>
        </button>
      }
    >
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
                  <td
                    colSpan={7}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Carregando condições...
                  </td>
                </tr>
              ) : conditions.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Nenhuma condição configurada.
                  </td>
                </tr>
              ) : (
                conditions.map((item) => (
                  <tr
                    key={item.id}
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
                    <td
                      style={{
                        padding: 'var(--space-3)',
                        fontWeight: item.additionalDiscount > 0 ? 600 : 400,
                        color: item.additionalDiscount > 0 ? 'var(--color-success)' : 'inherit',
                      }}
                    >
                      {item.additionalDiscount > 0 ? `${item.additionalDiscount}% OFF` : '—'}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={item.active} />
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleOpenEdit(item)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={14} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(item.id, item.name)}
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
      </div>

      {/* Modal Padronizado de Criação / Edição */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={editing ? 'Editar Condição de Pagamento' : 'Nova Condição de Pagamento'}
        subtitle="Configure os parâmetros de juros, parcelas e descontos para orçamentos"
        icon="credit-card"
        maxWidth={520}
        footer={
          <>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setShowModal(false)}
              disabled={saving}
              style={{ fontSize: 12, padding: '6px 16px' }}
            >
              Cancelar
            </button>
            <button
              type="submit"
              form="form-payment-condition"
              className="btn btn-primary"
              disabled={saving}
              style={{ fontSize: 12, padding: '6px 18px', gap: 6 }}
            >
              <Icon name="check" size={14} />
              <span>{saving ? 'Gravando...' : 'Salvar Condição'}</span>
            </button>
          </>
        }
      >
        {error && (
          <div
            style={{
              padding: 'var(--space-3)',
              background: 'rgba(239, 68, 68, 0.1)',
              color: 'var(--color-error)',
              borderRadius: 'var(--radius-md)',
              fontSize: 'var(--text-xs)',
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
            }}
          >
            <Icon name="alert-circle" size={14} />
            <span>{error}</span>
          </div>
        )}

        <form id="form-payment-condition" onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          <Input
            label="Nome da Regra"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex: Pix com 5% de desconto"
            leftIcon="tag"
          />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Select
              label="Método"
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(e.target.value as any)}
              leftIcon="credit-card"
              options={[
                { value: 'pix', label: 'Pix' },
                { value: 'credit_card', label: 'Cartão de Crédito' },
                { value: 'debit_card', label: 'Cartão de Débito' },
                { value: 'boleto', label: 'Boleto Bancário' },
              ]}
            />

            <Input
              label="Máximo de Parcelas"
              type="number"
              min="1"
              max="36"
              value={maxInstallments}
              onChange={(e) => setMaxInstallments(e.target.value)}
              leftIcon="hash"
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Input
              label="Desconto Extra (%)"
              type="number"
              step="0.1"
              value={additionalDiscount}
              onChange={(e) => setAdditionalDiscount(e.target.value)}
              placeholder="0"
              leftIcon="percent"
            />

            <Input
              label="Taxa de Juros (% a.m.)"
              type="number"
              step="0.1"
              value={interestRate}
              onChange={(e) => {
                setInterestRate(e.target.value);
                setInterestFree(parseFloat(e.target.value) === 0);
              }}
              placeholder="0"
              leftIcon="percent"
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)', paddingTop: 'var(--space-2)' }}>
            <Switch
              label="Sem Juros"
              description="Habilita parcelamento sem cobrança de taxa"
              checked={interestFree}
              onChange={(val) => {
                setInterestFree(val);
                if (val) setInterestRate('0');
              }}
            />

            <Switch
              label="Condição Ativa"
              description="Visível para o gerador de orçamento"
              checked={active}
              onChange={setActive}
            />
          </div>
        </form>
      </Modal>
    </AdminShell>
  );
}
