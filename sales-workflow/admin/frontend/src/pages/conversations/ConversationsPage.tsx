import { useEffect, useState } from 'react';
import { Icon, Card } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type ConversationItem } from '@/services/adminApiClient';

export function ConversationsPage() {
  const [conversations, setConversations] = useState<(ConversationItem & { customerName?: string })[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedConversation, setSelectedConversation] = useState<ConversationItem | null>(null);

  useEffect(() => {
    loadConversations();
  }, []);

  async function loadConversations() {
    setLoading(true);
    try {
      const data = await adminApi.listConversations();
      const withCustomers = await Promise.all(
        data.map(async (c) => {
          if (c.customerId) {
            try {
              const customer = await adminApi.getCustomer(c.customerId);
              return { ...c, customerName: customer.name };
            } catch {
              return c;
            }
          }
          return c;
        })
      );
      setConversations(withCustomers);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Deseja realmente excluir esta conversa e todo o seu histórico?')) return;
    try {
      await adminApi.deleteConversation(id);
      loadConversations();
      if (selectedConversation?.id === id) {
        setSelectedConversation(null);
      }
    } catch (err) {
      console.error(err);
      alert('Erro ao excluir conversa');
    }
  }

  return (
    <AdminShell
      title="Conversas & Histórico (Logs)"
      subtitle="Auditoria de atendimentos e sessões executadas pelo pipeline multiagente"
    >
      {selectedConversation ? (
        <div style={{ maxWidth: 900 }}>
          <div style={{ marginBottom: 'var(--space-4)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setSelectedConversation(null)}
              style={{ fontSize: 12, padding: '6px 14px', gap: 6 }}
            >
              <Icon name="arrow-left" size={14} />
              <span>Voltar para Lista</span>
            </button>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => handleDelete(selectedConversation.id)}
              style={{ fontSize: 12, padding: '6px 14px', color: 'var(--color-error)', gap: 6 }}
            >
              <Icon name="trash-2" size={14} />
              <span>Excluir Histórico</span>
            </button>
          </div>

          <Card
            title={`Sessão: ${selectedConversation.sessionId || selectedConversation.id}`}
            subtitle={`Iniciada em ${new Date(selectedConversation.createdAt).toLocaleString()} • Status: ${selectedConversation.status}`}
            style={{ marginBottom: 'var(--space-4)' }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
              {(!selectedConversation.messages || selectedConversation.messages.length === 0) ? (
                <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
                  Nenhuma mensagem registrada nesta conversa.
                </div>
              ) : (
                selectedConversation.messages.map((m) => {
                  const isUser = m.role.toLowerCase() === 'user';
                  return (
                    <div
                      key={m.id}
                      style={{
                        padding: 'var(--space-3) var(--space-4)',
                        borderRadius: 'var(--radius-lg)',
                        background: isUser ? 'var(--color-primary-glow)' : 'var(--color-surface-offset)',
                        border: `1px solid ${isUser ? 'rgba(var(--color-primary-rgb, 59, 130, 246), 0.2)' : 'var(--color-border)'}`,
                        alignSelf: isUser ? 'flex-end' : 'flex-start',
                        maxWidth: '85%',
                        fontSize: 'var(--text-xs)',
                      }}
                    >
                      <div
                        style={{
                          fontSize: 10,
                          fontWeight: 600,
                          color: isUser ? 'var(--color-primary)' : 'var(--color-text-muted)',
                          marginBottom: 4,
                          display: 'flex',
                          alignItems: 'center',
                          gap: 6,
                        }}
                      >
                        <Icon name={isUser ? 'user' : 'bot'} size={12} />
                        <span>{isUser ? 'CLIENTE' : m.role.toUpperCase()}</span>
                        <span>•</span>
                        <span>{new Date(m.createdAt).toLocaleTimeString()}</span>
                      </div>
                      <div style={{ whiteSpace: 'pre-wrap', lineHeight: 1.5, color: 'var(--color-text)' }}>
                        {m.content}
                      </div>
                    </div>
                  );
                })
              )}
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
                  <th style={{ padding: 'var(--space-3)' }}>Data / Hora</th>
                  <th style={{ padding: 'var(--space-3)' }}>ID da Sessão</th>
                  <th style={{ padding: 'var(--space-3)' }}>Cliente</th>
                  <th style={{ padding: 'var(--space-3)' }}>Status</th>
                  <th style={{ padding: 'var(--space-3)' }}>Mensagens</th>
                  <th style={{ padding: 'var(--space-3)', textAlign: 'right' }}>Ações</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td
                      colSpan={6}
                      style={{
                        padding: 'var(--space-8)',
                        textAlign: 'center',
                        color: 'var(--color-text-muted)',
                      }}
                    >
                      Carregando histórico de conversas...
                    </td>
                  </tr>
                ) : conversations.length === 0 ? (
                  <tr>
                    <td
                      colSpan={6}
                      style={{
                        padding: 'var(--space-8)',
                        textAlign: 'center',
                        color: 'var(--color-text-muted)',
                      }}
                    >
                      Nenhuma conversa registrada.
                    </td>
                  </tr>
                ) : (
                  conversations.map((c) => (
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
                      <td style={{ padding: 'var(--space-3)', whiteSpace: 'nowrap' }}>
                        {new Date(c.createdAt).toLocaleString()}
                      </td>
                      <td style={{ padding: 'var(--space-3)', fontFamily: 'monospace', color: 'var(--color-primary)' }}>
                        {c.sessionId ? c.sessionId.substring(0, 12) + '...' : c.id.substring(0, 8)}
                      </td>
                      <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                        {c.customerName || 'Anônimo'}
                      </td>
                      <td style={{ padding: 'var(--space-3)' }}>
                        <StatusBadge
                          status={c.status === 'closed' || c.status === 'done' || c.status === 'active'}
                          trueText={c.status.toUpperCase()}
                          falseText={c.status.toUpperCase()}
                        />
                      </td>
                      <td style={{ padding: 'var(--space-3)' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: 4,
                            fontSize: 11,
                            background: 'var(--color-surface-offset)',
                            border: '1px solid var(--color-border)',
                          }}
                        >
                          {c.messages?.length || 0} msgs
                        </span>
                      </td>
                      <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                          <button
                            type="button"
                            className="btn btn-ghost"
                            onClick={async () => {
                              const full = await adminApi.getConversation(c.id);
                              setSelectedConversation(full);
                            }}
                            style={{ padding: '4px 8px' }}
                            title="Ver Chat"
                          >
                            <Icon name="message-square" size={14} />
                          </button>
                          <button
                            type="button"
                            className="btn btn-ghost"
                            onClick={() => handleDelete(c.id)}
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
