import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type DiscountItem } from '@/services/adminApiClient';

export function DiscountListPage() {
  const navigate = useNavigate();
  const [discounts, setDiscounts] = useState<DiscountItem[]>([]);
  const [loading, setLoading] = useState(true);

  async function loadData() {
    setLoading(true);
    try {
      const data = await adminApi.listDiscounts();
      setDiscounts(data);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleDelete(id: string, code: string) {
    if (!window.confirm(`Deseja desativar o cupom "${code}"?`)) return;
    try {
      await adminApi.deleteDiscount(id);
      loadData();
    } catch (err: any) {
      alert(err.message);
    }
  }

  function isExpired(d: DiscountItem) {
    const now = new Date();
    if (new Date(d.validFrom) > now) return 'Agendado';
    if (d.validUntil && new Date(d.validUntil) < now) return 'Expirado';
    return 'Vigente';
  }

  return (
    <AdminShell
      title="Descontos & Cupons Promocionais"
      subtitle="Regras comerciais e cupons utilizados pelo QuoteAgent e SalesAdvisorAgent para aplicar ofertas"
      action={
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => navigate('/discounts/new')}
          style={{ fontSize: 11 }}
        >
          <Icon name="plus" size={14} />
          <span>Novo Cupom</span>
        </button>
      }
    >
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
              <th style={{ padding: 'var(--space-3)' }}>Código</th>
              <th style={{ padding: 'var(--space-3)' }}>Nome da Promoção</th>
              <th style={{ padding: 'var(--space-3)' }}>Desconto</th>
              <th style={{ padding: 'var(--space-3)' }}>Vigência</th>
              <th style={{ padding: 'var(--space-3)' }}>Pagamento Elegível</th>
              <th style={{ padding: 'var(--space-3)' }}>Usos</th>
              <th style={{ padding: 'var(--space-3)' }}>Status</th>
              <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Carregando cupons...
                </td>
              </tr>
            ) : discounts.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Nenhum cupom cadastrado.
                </td>
              </tr>
            ) : (
              discounts.map((d) => {
                const validity = isExpired(d);
                return (
                  <tr key={d.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', fontWeight: 700, color: 'var(--color-primary)' }}>
                      {d.code}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <div style={{ fontWeight: 600 }}>{d.name}</div>
                      {d.description && <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{d.description}</div>}
                    </td>
                    <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                      {d.discountType === 'percentage' ? `${d.discountValue}%` : `R$ ${d.discountValue.toFixed(2)}`}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span
                        style={{
                          fontSize: 10,
                          fontWeight: 600,
                          padding: '2px 6px',
                          borderRadius: 4,
                          background:
                            validity === 'Vigente'
                              ? 'rgba(34, 197, 94, 0.1)'
                              : validity === 'Expirado'
                              ? 'rgba(239, 68, 68, 0.1)'
                              : 'rgba(234, 179, 8, 0.1)',
                          color:
                            validity === 'Vigente'
                              ? 'var(--color-success)'
                              : validity === 'Expirado'
                              ? 'var(--color-danger)'
                              : 'var(--color-warning)',
                        }}
                      >
                        {validity}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                      {d.paymentMethods.length > 0 ? d.paymentMethods.join(', ') : 'Todos'}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      {d.currentUses} {d.maxUses ? `/ ${d.maxUses}` : 'usos'}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={d.active} />
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-2)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => navigate(`/discounts/${d.id}`)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={13} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(d.id, d.code)}
                          style={{ padding: '4px 8px', color: 'var(--color-danger)' }}
                          title="Desativar"
                        >
                          <Icon name="x" size={13} />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </AdminShell>
  );
}
