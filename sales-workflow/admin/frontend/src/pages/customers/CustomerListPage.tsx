import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon, Input, Pagination } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type CustomerItem } from '@/services/adminApiClient';

export function CustomerListPage() {
  const navigate = useNavigate();
  const [customers, setCustomers] = useState<CustomerItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [total, setTotal] = useState(0);

  async function loadData() {
    setLoading(true);
    try {
      const res = await adminApi.listCustomers({
        search: search.trim() || undefined,
        page,
        pageSize,
      });
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
  }, [page, pageSize]);

  function handleSearchSubmit(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    loadData();
  }

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
          style={{ fontSize: 11, gap: 6 }}
        >
          <Icon name="plus" size={14} />
          <span>Cadastrar Cliente</span>
        </button>
      }
    >
      {/* Barra de Filtros */}
      <form
        onSubmit={handleSearchSubmit}
        className="surface-card"
        style={{
          padding: 'var(--space-3) var(--space-4)',
          marginBottom: 'var(--space-4)',
          display: 'flex',
          gap: 'var(--space-3)',
          alignItems: 'center',
        }}
      >
        <div style={{ flex: 1 }}>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Buscar por nome, e-mail, telefone, CPF..."
            leftIcon="search"
          />
        </div>
        <button
          type="submit"
          className="btn btn-secondary"
          style={{ height: '36px', padding: '0 16px', gap: '6px' }}
        >
          <Icon name="search" size={14} />
          <span>Buscar</span>
        </button>
      </form>

      {/* Grid de Clientes */}
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
                  <td
                    colSpan={7}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Carregando clientes...
                  </td>
                </tr>
              ) : customers.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Nenhum cliente encontrado.
                  </td>
                </tr>
              ) : (
                customers.map((c) => (
                  <tr
                    key={c.id}
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
                    <td style={{ padding: 'var(--space-3)' }}>
                      <div style={{ fontWeight: 600, color: 'var(--color-text)' }}>{c.name}</div>
                      {c.notes && (
                        <div
                          style={{
                            fontSize: 10,
                            color: 'var(--color-text-muted)',
                            maxWidth: 200,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                          title={c.notes}
                        >
                          {c.notes}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <div>{c.email}</div>
                      {c.phone && <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{c.phone}</div>}
                    </td>
                    <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace' }}>
                      {c.documentNumber ? (
                        <span>
                          <span style={{ fontSize: 10, color: 'var(--color-text-muted)', marginRight: 4 }}>
                            {c.documentType?.toUpperCase()}
                          </span>
                          {c.documentNumber}
                        </span>
                      ) : (
                        '—'
                      )}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span
                        style={{
                          fontSize: 10,
                          fontWeight: 600,
                          padding: '2px 6px',
                          borderRadius: 4,
                          background: 'var(--color-surface-offset)',
                          border: '1px solid var(--color-border)',
                        }}
                      >
                        {c.customerType === 'business' ? 'PJ' : 'PF'}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>
                        {c.addresses?.length ? `${c.addresses.length} cadastrado(s)` : 'Nenhum'}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={c.active} />
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => navigate(`/customers/${c.id}`)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={14} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(c.id, c.name)}
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

        {total > pageSize && (
          <Pagination
            currentPage={page}
            totalItems={total}
            pageSize={pageSize}
            onPageChange={(newPage) => setPage(newPage)}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
            pageSizeOptions={[15, 30, 50, 100]}
          />
        )}
      </div>
    </AdminShell>
  );
}
