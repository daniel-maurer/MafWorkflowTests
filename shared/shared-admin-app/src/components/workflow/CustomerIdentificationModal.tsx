import { useEffect, useState } from 'react';
import { Icon } from '../Icon';
import { apiClient } from '../../services/apiClient';

export interface CustomerData {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  documentNumber?: string | null;
  addresses?: Array<{
    street: string;
    number: string;
    neighborhood: string;
    city: string;
    state: string;
  }>;
}

interface CustomerIdentificationModalProps {
  isOpen: boolean;
  workflowTitle?: string;
  currentCustomer?: CustomerData | null;
  onConfirm: (customer: CustomerData | null) => void;
  onClose?: () => void;
}

export function CustomerIdentificationModal({
  isOpen,
  workflowTitle = 'Workflow',
  currentCustomer,
  onConfirm,
  onClose,
}: CustomerIdentificationModalProps) {
  const [identifier, setIdentifier] = useState('');
  const [loading, setLoading] = useState(false);
  const [foundCustomer, setFoundCustomer] = useState<CustomerData | null>(currentCustomer ?? null);
  const [recentCustomers, setRecentCustomers] = useState<CustomerData[]>([]);
  const [searchDone, setSearchDone] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setFoundCustomer(currentCustomer ?? null);
      setIdentifier(currentCustomer?.email || currentCustomer?.id || '');
      setSearchDone(false);

      // Carregar os clientes cadastrados recentemente no Admin para seleção rápida
      apiClient.searchCustomers().then((res: any) => {
        if (res?.items && Array.isArray(res.items)) {
          setRecentCustomers(
            res.items.slice(0, 4).map((c: any) => ({
              id: c.id,
              name: c.name,
              email: c.email,
              phone: c.phone,
              documentNumber: c.documentNumber,
              addresses: c.addresses,
            }))
          );
        }
      }).catch(() => {
        // Mock ou fallback silencioso
      });
    }
  }, [isOpen, currentCustomer]);

  if (!isOpen) return null;

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    if (!identifier.trim()) return;

    setLoading(true);
    setSearchDone(false);
    try {
      const cust = await apiClient.findCustomer(identifier.trim());
      if (cust && cust.id) {
        setFoundCustomer({
          id: cust.id,
          name: cust.name,
          email: cust.email,
          phone: cust.phone,
          documentNumber: cust.documentNumber,
          addresses: cust.addresses,
        });
      } else {
        setFoundCustomer(null);
      }
      setSearchDone(true);
    } catch (err) {
      console.error('Erro ao buscar cliente:', err);
      setFoundCustomer(null);
      setSearchDone(true);
    } finally {
      setLoading(false);
    }
  }

  function handleSelectRecent(cust: CustomerData) {
    setFoundCustomer(cust);
    setIdentifier(cust.email || cust.id);
    setSearchDone(true);
  }

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.65)',
        backdropFilter: 'blur(4px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '1rem',
      }}
    >
      <div
        style={{
          backgroundColor: 'var(--color-surface)',
          border: '1px solid var(--color-border)',
          borderRadius: 'var(--radius-lg)',
          width: '100%',
          maxWidth: '560px',
          boxShadow: 'var(--shadow-lg)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          color: 'var(--color-text)',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: 'var(--space-4) var(--space-5)',
            borderBottom: '1px solid var(--color-border)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: 'var(--color-surface-offset)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
            <div
              style={{
                width: 36,
                height: 36,
                borderRadius: 'var(--radius-md)',
                backgroundColor: 'var(--color-primary-glow)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--color-primary)',
              }}
            >
              <Icon name="user" size={18} />
            </div>
            <div>
              <h2 style={{ fontSize: 'var(--text-base)', fontWeight: 600, margin: 0, color: 'var(--color-text)' }}>
                Identificação do Cliente
              </h2>
              <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>
                {workflowTitle}
              </span>
            </div>
          </div>
          {onClose && (
            <button
              onClick={onClose}
              className="btn btn-ghost"
              style={{
                padding: 'var(--space-2)',
                color: 'var(--color-text-muted)',
              }}
            >
              <Icon name="x" size={16} />
            </button>
          )}
        </div>

        {/* Body */}
        <div style={{ padding: 'var(--space-5)', display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          <p style={{ margin: 0, fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', lineHeight: 1.5 }}>
            Informe o <strong>ID, Nome, E-mail, Telefone ou CPF</strong> do cliente para carregar o histórico e endereços cadastrados no sistema.
          </p>

          <form onSubmit={handleSearch} style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <div style={{ flex: 1, position: 'relative' }}>
              <input
                type="text"
                className="input"
                value={identifier}
                onChange={(e) => setIdentifier(e.target.value)}
                placeholder="Ex: Carlos, carlos@email.com, ou GUID..."
                style={{
                  width: '100%',
                  fontSize: 'var(--text-xs)',
                  height: 36,
                }}
                autoFocus
              />
            </div>
            <button
              type="submit"
              disabled={loading || !identifier.trim()}
              className="btn btn-primary"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 'var(--space-2)',
                fontSize: 'var(--text-xs)',
                height: 36,
              }}
            >
              {loading ? (
                <span>Buscando...</span>
              ) : (
                <>
                  <Icon name="search" size={14} />
                  <span>Buscar</span>
                </>
              )}
            </button>
          </form>

          {/* Resultado da Busca */}
          {foundCustomer ? (
            <div
              style={{
                backgroundColor: 'var(--color-success-bg)',
                border: '1px solid var(--color-success)',
                borderRadius: 'var(--radius-md)',
                padding: 'var(--space-4)',
                display: 'flex',
                flexDirection: 'column',
                gap: 'var(--space-2)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 600,
                    textTransform: 'uppercase',
                    color: 'var(--color-success)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                  }}
                >
                  <Icon name="check" size={12} />
                  <span>Cliente Encontrado no Admin</span>
                </span>
                <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>ID: {foundCustomer.id.slice(0, 8)}...</span>
              </div>
              <div style={{ fontSize: 'var(--text-sm)', fontWeight: 600, color: 'var(--color-text)' }}>
                {foundCustomer.name}
              </div>
              <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', display: 'flex', flexWrap: 'wrap', gap: 'var(--space-3)' }}>
                {foundCustomer.email && <span>📧 {foundCustomer.email}</span>}
                {foundCustomer.phone && <span>📱 {foundCustomer.phone}</span>}
                {foundCustomer.documentNumber && <span>📄 {foundCustomer.documentNumber}</span>}
              </div>
              {foundCustomer.addresses && foundCustomer.addresses.length > 0 && (
                <div style={{ fontSize: '11px', color: 'var(--color-text-muted)', borderTop: '1px dashed var(--color-border)', paddingTop: 'var(--space-2)', marginTop: 'var(--space-1)' }}>
                  📍 {foundCustomer.addresses[0].street}, {foundCustomer.addresses[0].number} - {foundCustomer.addresses[0].city}/{foundCustomer.addresses[0].state}
                </div>
              )}
            </div>
          ) : searchDone ? (
            <div
              style={{
                backgroundColor: 'var(--color-error-bg)',
                border: '1px solid var(--color-error)',
                borderRadius: 'var(--radius-md)',
                padding: 'var(--space-3)',
                fontSize: 'var(--text-xs)',
                color: 'var(--color-error)',
                display: 'flex',
                alignItems: 'center',
                gap: 'var(--space-2)',
              }}
            >
              <Icon name="alert-circle" size={16} />
              <span>Nenhum cliente cadastrado foi encontrado com esse identificador.</span>
            </div>
          ) : null}

          {/* Sugestões rápidas de clientes existentes */}
          {!foundCustomer && recentCustomers.length > 0 && (
            <div>
              <div style={{ fontSize: '11px', color: 'var(--color-text-muted)', marginBottom: 'var(--space-2)', fontWeight: 500 }}>
                Ou selecione um cliente cadastrado recentemente:
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
                {recentCustomers.map((c: any) => (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => handleSelectRecent(c)}
                    style={{
                      background: 'var(--color-surface-offset)',
                      border: '1px solid var(--color-border)',
                      borderRadius: 'var(--radius-md)',
                      padding: 'var(--space-2) var(--space-3)',
                      textAlign: 'left',
                      color: 'var(--color-text)',
                      cursor: 'pointer',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      fontSize: 'var(--text-xs)',
                      transition: 'border-color var(--transition)',
                    }}
                  >
                    <span style={{ fontWeight: 600 }}>{c.name}</span>
                    <span style={{ color: 'var(--color-text-muted)', fontSize: '11px' }}>{c.email || c.phone || 'Sem contato'}</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: 'var(--space-3) var(--space-5)',
            borderTop: '1px solid var(--color-border)',
            backgroundColor: 'var(--color-surface-offset)',
            display: 'flex',
            justifyContent: 'flex-end',
            gap: 'var(--space-3)',
          }}
        >
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onConfirm(null)}
            style={{ fontSize: 'var(--text-xs)' }}
          >
            Continuar como Cliente Novo
          </button>

          <button
            type="button"
            disabled={!foundCustomer}
            className="btn btn-primary"
            onClick={() => onConfirm(foundCustomer)}
            style={{ fontSize: 'var(--text-xs)' }}
          >
            Iniciar com {foundCustomer ? foundCustomer.name.split(' ')[0] : 'Cliente Selecionado'}
          </button>
        </div>
      </div>
    </div>
  );
}
