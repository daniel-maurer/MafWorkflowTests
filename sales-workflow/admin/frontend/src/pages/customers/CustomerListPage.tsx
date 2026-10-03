import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type CustomerItem } from '@/services/adminApiClient';

export function CustomerListPage() {
  const navigate = useNavigate();
  const [customers, setCustomers] = useState<CustomerItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [total, setTotal] = useState(0);

  async function loadData() {
    setLoading(true);
    try {
      const res = await adminApi.listCustomers({ search: search || undefined, pageSize: 50 });
      setCustomers(res.items);
      setTotal(res.total);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleDelete(id: string, name: string) {
    if (!window.confirm(`Deseja desativar o cliente "${name}"?`)) return;
    try {
      await adminApi.deleteCustomer(id);
      loadData();
    } catch (err: any) {
      alert(err.message);
    }
  }

  return (
    <AdminShell
      title="Clientes & Contatos"
      subtitle={`${total} cliente(s) cadastrados — base unificada compartilhada entre todos os módulos`}
      action={
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => navigate('/customers/new')}
          style={{ fontSize: 11 }}
        >
          <Icon name="plus" size={14} />
          <span>Cadastrar Cliente</span>
        </button>
      }
    >
      {/* Search Bar */}
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
            placeholder="Buscar por nome, e-mail, telefone, CPF..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && loadData()}
            style={{ width: '100%', fontSize: 'var(--text-xs)', height: 32 }}
          />
        </div>
        <button type="button" className="btn btn-secondary" onClick={loadData} style={{ fontSize: 11, height: 32 }}>
          Buscar
        </button>
      </div>

      {/* Table */}
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-xs)' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-offset)', borderBottom: '1px solid var(--color-border)', textAlign: 'left' }}>
              <th style={{ padding: 'var(--space-3)' }}>Nome / Razão Social</th>
              <th style={{ padding: 'var(--space-3)' }}>Contato</th>
              <th style={{ padding: 'var(--space-3)' }}>Documento</th>
              <th style={{ padding: 'var(--space-3)' }}>Tipo</th>
              <th style={{ padding: 'var(--space-3)' }}>Endereços</th>
              <th style={{ padding: 'var(--space-3)' }}>Status</th>
              <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Carregando clientes...
                </td>
              </tr>
            ) : customers.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  Nenhum cliente encontrado.
                </td>
              </tr>
            ) : (
              customers.map((c) => (
                <tr key={c.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <div style={{ fontWeight: 600 }}>{c.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{c.email}</div>
                  </td>
                  <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                    {c.phone || '—'}
                  </td>
                  <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace' }}>
                    {c.documentNumber || '—'}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <span
                      style={{
                        fontSize: 10,
                        padding: '2px 6px',
                        borderRadius: 4,
                        background: 'var(--color-surface-offset)',
                        border: '1px solid var(--color-border)',
                      }}
                    >
                      {c.customerType === 'business' ? 'Pessoa Jurídica' : 'Pessoa Física'}
                    </span>
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    {c.addresses?.length > 0 ? (
                      <span title={c.addresses.map((a) => `${a.label}: ${a.street}, ${a.number} - ${a.city}/${a.state}`).join('\n')}>
                        {c.addresses.length} cadastrado(s)
                      </span>
                    ) : (
                      <span style={{ color: 'var(--color-text-faint)' }}>Nenhum</span>
                    )}
                  </td>
                  <td style={{ padding: 'var(--space-3)' }}>
                    <StatusBadge status={c.active} />
                  </td>
                  <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 'var(--space-2)' }}>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => navigate(`/customers/${c.id}`)}
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
    </AdminShell>
  );
}
