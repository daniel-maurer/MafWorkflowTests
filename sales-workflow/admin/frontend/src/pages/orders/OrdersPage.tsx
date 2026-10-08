import { useEffect, useState } from 'react';
import { Icon, Card } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type OrderItem, type CustomerItem } from '@/services/adminApiClient';

export function OrdersPage() {
  const [orders, setOrders] = useState<(OrderItem & { customer?: CustomerItem })[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedOrder, setSelectedOrder] = useState<OrderItem | null>(null);

  useEffect(() => {
    loadOrders();
  }, []);

  async function loadOrders() {
    setLoading(true);
    try {
      const data = await adminApi.listOrders();
      const withCustomers = await Promise.all(
        data.map(async (o) => {
          try {
            const customer = await adminApi.getCustomer(o.customerId);
            return { ...o, customer };
          } catch {
            return o;
          }
        })
      );
      setOrders(withCustomers);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Deseja realmente excluir este pedido?')) return;
    try {
      await adminApi.deleteOrder(id);
      loadOrders();
      if (selectedOrder?.id === id) {
        setSelectedOrder(null);
      }
    } catch (err) {
      console.error(err);
      alert('Erro ao excluir pedido');
    }
  }

  return (
    <AdminShell
      title="Pedidos & Fechamentos"
      subtitle="Pedidos concluídos pelos clientes através da interação com o QuoteAgent"
    >
      {selectedOrder ? (
        <div style={{ maxWidth: 880 }}>
          <div style={{ marginBottom: 'var(--space-4)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setSelectedOrder(null)}
              style={{ fontSize: 12, padding: '6px 14px', gap: 6 }}
            >
              <Icon name="arrow-left" size={14} />
              <span>Voltar para Lista</span>
            </button>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => handleDelete(selectedOrder.id)}
              style={{ fontSize: 12, padding: '6px 14px', color: 'var(--color-error)', gap: 6 }}
            >
              <Icon name="trash-2" size={14} />
              <span>Excluir Pedido</span>
            </button>
          </div>

          <Card
            title={`Pedido #${selectedOrder.id.substring(0, 8).toUpperCase()}`}
            subtitle={`Emitido em ${new Date(selectedOrder.createdAt).toLocaleString()}`}
            style={{ marginBottom: 'var(--space-4)' }}
          >
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-4)', marginBottom: 'var(--space-5)' }}>
              <div>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)', display: 'block' }}>Status</span>
                <span style={{ fontWeight: 600, fontSize: 'var(--text-xs)' }}>{selectedOrder.status}</span>
              </div>
              <div>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)', display: 'block' }}>Forma de Entrega</span>
                <span style={{ fontWeight: 600, fontSize: 'var(--text-xs)' }}>{selectedOrder.deliveryMethod || 'Não informada'}</span>
              </div>
              <div>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)', display: 'block' }}>Pagamento</span>
                <span style={{ fontWeight: 600, fontSize: 'var(--text-xs)' }}>{selectedOrder.paymentMethod || 'Não informada'}</span>
              </div>
              <div>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)', display: 'block' }}>Valor Total</span>
                <span style={{ fontWeight: 700, fontSize: 'var(--text-sm)', color: 'var(--color-primary)' }}>
                  {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(selectedOrder.totalAmount)}
                </span>
              </div>
            </div>

            <h4 style={{ fontSize: 'var(--text-xs)', fontWeight: 600, marginBottom: 'var(--space-3)' }}>
              Itens do Pedido ({selectedOrder.items?.length || 0})
            </h4>

            <div style={{ overflowX: 'auto', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
                <thead>
                  <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
                    <th style={{ padding: 'var(--space-3)' }}>SKU</th>
                    <th style={{ padding: 'var(--space-3)' }}>Produto</th>
                    <th style={{ padding: 'var(--space-3)' }}>Qtd</th>
                    <th style={{ padding: 'var(--space-3)' }}>Preço Unit.</th>
                    <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Total</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedOrder.items?.map((i) => (
                    <tr key={i.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                      <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', color: 'var(--color-primary)' }}>{i.sku}</td>
                      <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>{i.name}</td>
                      <td style={{ padding: 'var(--space-3)' }}>{i.quantity} un</td>
                      <td style={{ padding: 'var(--space-3)' }}>
                        {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(i.unitPrice)}
                      </td>
                      <td style={{ padding: 'var(--space-3)', textAlign: 'right', fontWeight: 600 }}>
                        {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(i.totalPrice)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      ) : (
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
                  <th style={{ padding: 'var(--space-3)' }}>Data</th>
                  <th style={{ padding: 'var(--space-3)' }}>ID Pedido</th>
                  <th style={{ padding: 'var(--space-3)' }}>Cliente</th>
                  <th style={{ padding: 'var(--space-3)' }}>Status</th>
                  <th style={{ padding: 'var(--space-3)' }}>Entrega</th>
                  <th style={{ padding: 'var(--space-3)' }}>Total</th>
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
                      Carregando pedidos...
                    </td>
                  </tr>
                ) : orders.length === 0 ? (
                  <tr>
                    <td
                      colSpan={7}
                      style={{
                        padding: 'var(--space-8)',
                        textAlign: 'center',
                        color: 'var(--color-text-muted)',
                      }}
                    >
                      Nenhum pedido finalizado encontrado.
                    </td>
                  </tr>
                ) : (
                  orders.map((o) => (
                    <tr
                      key={o.id}
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
                      <td style={{ padding: 'var(--space-3)', whiteSpace: 'nowrap' }}>
                        {new Date(o.createdAt).toLocaleDateString()} {new Date(o.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </td>
                      <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', fontWeight: 600, color: 'var(--color-primary)' }}>
                        #{o.id.substring(0, 8).toUpperCase()}
                      </td>
                      <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                        {o.customer?.name || 'Cliente'}
                      </td>
                      <td style={{ padding: 'var(--space-3)' }}>
                        <StatusBadge
                          status={o.status.toLowerCase() === 'confirmed' || o.status.toLowerCase() === 'completed'}
                          trueText={o.status.toUpperCase()}
                          falseText={o.status.toUpperCase()}
                        />
                      </td>
                      <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                        {o.deliveryMethod || '—'}
                      </td>
                      <td style={{ padding: 'var(--space-3)', fontWeight: 700 }}>
                        {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(o.totalAmount)}
                      </td>
                      <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                          <button
                            type="button"
                            className="btn btn-ghost"
                            onClick={() => setSelectedOrder(o)}
                            style={{ padding: '4px 8px' }}
                            title="Ver Detalhes"
                          >
                            <Icon name="eye" size={14} />
                          </button>
                          <button
                            type="button"
                            className="btn btn-ghost"
                            onClick={() => handleDelete(o.id)}
                            style={{ padding: '4px 8px', color: 'var(--color-error)' }}
                            title="Excluir"
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
      )}
    </AdminShell>
  );
}
