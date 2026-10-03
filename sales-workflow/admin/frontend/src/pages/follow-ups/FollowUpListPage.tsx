import { useEffect, useState } from 'react';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi, type FollowUpItem, type CustomerItem } from '@/services/adminApiClient';

export function FollowUpListPage() {
  const [items, setItems] = useState<FollowUpItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [moduleFilter, setModuleFilter] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [search, setSearch] = useState('');
  const [total, setTotal] = useState(0);

  // Modal para novo follow-up
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [customers, setCustomers] = useState<CustomerItem[]>([]);
  const [newCustomerId, setNewCustomerId] = useState('');
  const [newModule, setNewModule] = useState('sales');
  const [newType, setNewType] = useState('quote_reminder');
  const [newRefTitle, setNewRefTitle] = useState('');
  const [newScheduledFor, setNewScheduledFor] = useState(
    new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 16)
  );
  const [newChannel, setNewChannel] = useState('WhatsApp');
  const [newMessageText, setNewMessageText] = useState('');
  const [saving, setSaving] = useState(false);

  async function loadData() {
    setLoading(true);
    try {
      const res = await adminApi.listFollowUps({
        module: moduleFilter || undefined,
        status: statusFilter || undefined,
        search: search || undefined,
        pageSize: 100,
      });
      setItems(res.items);
      setTotal(res.total);
    } catch (err) {
      console.error('Erro ao carregar follow-ups:', err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, [moduleFilter, statusFilter]);

  async function openCreateModal() {
    try {
      const res = await adminApi.listCustomers({ pageSize: 50 });
      setCustomers(res.items);
      if (res.items.length > 0 && !newCustomerId) {
        setNewCustomerId(res.items[0].id);
      }
    } catch (err) {
      console.error('Erro ao carregar clientes:', err);
    }
    setShowCreateModal(true);
  }

  async function handleCreateFollowUp(e: React.FormEvent) {
    e.preventDefault();
    if (!newCustomerId) {
      alert('Selecione um cliente.');
      return;
    }
    setSaving(true);
    try {
      await adminApi.createFollowUp({
        customerId: newCustomerId,
        module: newModule,
        followUpType: newType,
        referenceTitle: newRefTitle,
        scheduledFor: new Date(newScheduledFor).toISOString(),
        channel: newChannel,
        messageText: newMessageText || 'Olá! Gostaria de acompanhar nosso atendimento anterior.',
      });
      setShowCreateModal(false);
      loadData();
    } catch (err: any) {
      alert(err.message || 'Erro ao agendar follow-up');
    } finally {
      setSaving(false);
    }
  }

  async function handleUpdateStatus(id: string, status: string) {
    try {
      await adminApi.updateFollowUpStatus(id, status);
      setItems((prev) =>
        prev.map((item) =>
          item.id === id ? { ...item, status, sentAt: status === 'Sent' ? new Date().toISOString() : item.sentAt } : item
        )
      );
    } catch (err: any) {
      alert(err.message || 'Erro ao atualizar status');
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm('Deseja excluir este agendamento de follow-up?')) return;
    try {
      await adminApi.deleteFollowUp(id);
      setItems((prev) => prev.filter((item) => item.id !== id));
      setTotal((t) => t - 1);
    } catch (err: any) {
      alert(err.message || 'Erro ao excluir');
    }
  }

  function getWhatsAppUrl(phone?: string | null, message?: string) {
    if (!phone) return null;
    const cleanPhone = phone.replace(/\D/g, '');
    const formattedPhone = cleanPhone.length <= 11 ? `55${cleanPhone}` : cleanPhone;
    return `https://api.whatsapp.com/send?phone=${formattedPhone}&text=${encodeURIComponent(message || '')}`;
  }

  const pendingCount = items.filter((f) => f.status === 'Pending').length;
  const sentCount = items.filter((f) => f.status === 'Sent' || f.status === 'Completed').length;
  const salesCount = items.filter((f) => f.module === 'sales').length;
  const vetCount = items.filter((f) => f.module === 'vet').length;

  return (
    <AdminShell
      title="Follow-Ups & Retornos"
      subtitle="Acompanhamento inteligente compartilhado entre Vendas, Veterinário e Suporte"
      action={
        <button
          type="button"
          className="btn btn-primary"
          onClick={openCreateModal}
          style={{ fontSize: 11 }}
        >
          <Icon name="plus" size={14} />
          <span>Agendar Follow-Up</span>
        </button>
      }
    >
      {/* Cards de Resumo */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-4)', marginBottom: 'var(--space-6)' }}>
        <div className="surface-card" style={{ padding: 'var(--space-4)' }}>
          <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', marginBottom: 4 }}>Total de Follow-Ups</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: 'var(--color-text)' }}>{total}</div>
          <div style={{ fontSize: 11, color: 'var(--color-text-faint)', marginTop: 4 }}>Registros no banco unificado</div>
        </div>

        <div className="surface-card" style={{ padding: 'var(--space-4)' }}>
          <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-warning)', marginBottom: 4 }}>Pendentes</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: 'var(--color-warning)' }}>{pendingCount}</div>
          <div style={{ fontSize: 11, color: 'var(--color-text-faint)', marginTop: 4 }}>Aguardando envio / retorno</div>
        </div>

        <div className="surface-card" style={{ padding: 'var(--space-4)' }}>
          <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-success)', marginBottom: 4 }}>Enviados / Concluídos</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: 'var(--color-success)' }}>{sentCount}</div>
          <div style={{ fontSize: 11, color: 'var(--color-text-faint)', marginTop: 4 }}>Lembretes disparados</div>
        </div>

        <div className="surface-card" style={{ padding: 'var(--space-4)' }}>
          <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-primary)', marginBottom: 4 }}>Origem dos Agendamentos</div>
          <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'baseline', marginTop: 4 }}>
            <span style={{ fontSize: 18, fontWeight: 700 }}>{salesCount} <small style={{ fontSize: 11, fontWeight: 400 }}>Vendas</small></span>
            <span style={{ color: 'var(--color-border)' }}>|</span>
            <span style={{ fontSize: 18, fontWeight: 700 }}>{vetCount} <small style={{ fontSize: 11, fontWeight: 400 }}>Vet</small></span>
          </div>
          <div style={{ fontSize: 11, color: 'var(--color-text-faint)', marginTop: 4 }}>Disparados pelos Agentes IA</div>
        </div>
      </div>

      {/* Barra de Filtros */}
      <div
        className="surface-card"
        style={{
          padding: 'var(--space-3) var(--space-4)',
          marginBottom: 'var(--space-4)',
          display: 'flex',
          gap: 'var(--space-3)',
          alignItems: 'center',
          flexWrap: 'wrap',
        }}
      >
        <div style={{ flex: 1, minWidth: 220, display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          <Icon name="search" size={15} />
          <input
            type="text"
            className="input"
            placeholder="Buscar por cliente, referência ou mensagem..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && loadData()}
            style={{ width: '100%', fontSize: 'var(--text-xs)', height: 32 }}
          />
        </div>

        {/* Módulo */}
        <select
          className="input"
          value={moduleFilter}
          onChange={(e) => setModuleFilter(e.target.value)}
          style={{ width: 140, fontSize: 'var(--text-xs)', height: 32 }}
        >
          <option value="">Todos os Módulos</option>
          <option value="sales">Vendas (Sales)</option>
          <option value="vet">Veterinário (Vet)</option>
          <option value="support">Suporte</option>
        </select>

        {/* Status */}
        <select
          className="input"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          style={{ width: 140, fontSize: 'var(--text-xs)', height: 32 }}
        >
          <option value="">Todos os Status</option>
          <option value="Pending">Pendente</option>
          <option value="Sent">Enviado</option>
          <option value="Completed">Concluído</option>
          <option value="Cancelled">Cancelado</option>
        </select>

        <button type="button" className="btn btn-secondary" onClick={loadData} style={{ height: 32, fontSize: 11 }}>
          <Icon name="rotate-ccw" size={12} />
          <span>Atualizar</span>
        </button>
      </div>

      {/* Tabela de Follow-Ups */}
      <div className="surface-card" style={{ overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
            Carregando agendamentos de follow-up...
          </div>
        ) : items.length === 0 ? (
          <div style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
            Nenhum follow-up encontrado com os filtros aplicados.
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: 'var(--text-xs)' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--color-border)', background: 'var(--color-surface-offset)' }}>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Módulo</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Cliente</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Tipo / Referência</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Data Agendada</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Mensagem Sugerida</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)' }}>Status</th>
                  <th style={{ padding: 'var(--space-3) var(--space-4)', textAlign: 'right' }}>Ações</th>
                </tr>
              </thead>
              <tbody>
                {items.map((f) => {
                  const scheduledDate = new Date(f.scheduledFor);
                  const isOverdue = f.status === 'Pending' && scheduledDate.getTime() < Date.now();
                  const waUrl = getWhatsAppUrl(f.customerPhone, f.messageText);

                  return (
                    <tr
                      key={f.id}
                      style={{
                        borderBottom: '1px solid var(--color-border)',
                        background: isOverdue ? 'rgba(239, 68, 68, 0.04)' : undefined,
                      }}
                    >
                      {/* Módulo */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)' }}>
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 4,
                            padding: '2px 8px',
                            borderRadius: 'var(--radius-full)',
                            fontSize: 10,
                            fontWeight: 600,
                            textTransform: 'uppercase',
                            background:
                              f.module === 'vet'
                                ? 'rgba(16, 185, 129, 0.15)'
                                : f.module === 'sales'
                                ? 'rgba(59, 130, 246, 0.15)'
                                : 'rgba(245, 158, 11, 0.15)',
                            color:
                              f.module === 'vet'
                                ? '#10b981'
                                : f.module === 'sales'
                                ? '#3b82f6'
                                : '#f59e0b',
                          }}
                        >
                          <Icon name={f.module === 'vet' ? 'check' : 'tag'} size={11} />
                          {f.module === 'vet' ? 'Vet' : f.module === 'sales' ? 'Vendas' : f.module}
                        </span>
                      </td>

                      {/* Cliente */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)' }}>
                        <div style={{ fontWeight: 600 }}>{f.customerName}</div>
                        <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>
                          {f.customerPhone || f.customerEmail}
                        </div>
                      </td>

                      {/* Tipo / Referência */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)' }}>
                        <div style={{ fontWeight: 500, color: 'var(--color-text)' }}>
                          {f.referenceTitle || f.referenceId || f.followUpType}
                        </div>
                        <div style={{ fontSize: 10, color: 'var(--color-text-faint)', textTransform: 'uppercase' }}>
                          Tipo: {f.followUpType}
                        </div>
                      </td>

                      {/* Data Agendada */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                        <div>{scheduledDate.toLocaleDateString('pt-BR')} {scheduledDate.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</div>
                        {isOverdue && (
                          <div style={{ fontSize: 10, color: 'var(--color-danger)', fontWeight: 600 }}>Atrasado</div>
                        )}
                      </td>

                      {/* Mensagem */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)', maxWidth: 280 }}>
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--color-text-muted)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                          title={f.messageText}
                        >
                          "{f.messageText}"
                        </div>
                        <div style={{ fontSize: 10, color: 'var(--color-text-faint)', marginTop: 2 }}>
                          Canal: {f.channel}
                        </div>
                      </td>

                      {/* Status */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: 'var(--radius-full)',
                            fontSize: 10,
                            fontWeight: 600,
                            background:
                              f.status === 'Sent' || f.status === 'Completed'
                                ? 'rgba(16, 185, 129, 0.15)'
                                : f.status === 'Cancelled'
                                ? 'var(--color-surface-offset)'
                                : 'rgba(245, 158, 11, 0.15)',
                            color:
                              f.status === 'Sent' || f.status === 'Completed'
                                ? '#10b981'
                                : f.status === 'Cancelled'
                                ? 'var(--color-text-muted)'
                                : '#f59e0b',
                          }}
                        >
                          {f.status === 'Pending' ? 'Pendente' : f.status === 'Sent' ? 'Enviado' : f.status === 'Completed' ? 'Concluído' : 'Cancelado'}
                        </span>
                      </td>

                      {/* Ações */}
                      <td style={{ padding: 'var(--space-3) var(--space-4)', textAlign: 'right' }}>
                        <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end', alignItems: 'center' }}>
                          {/* Disparar WhatsApp se disponível */}
                          {waUrl && f.status === 'Pending' && (
                            <a
                              href={waUrl}
                              target="_blank"
                              rel="noreferrer"
                              className="btn btn-secondary"
                              style={{ padding: '4px 8px', fontSize: 10, color: '#10b981', borderColor: 'rgba(16, 185, 129, 0.4)' }}
                              title="Abrir WhatsApp com mensagem preenchida"
                              onClick={() => handleUpdateStatus(f.id, 'Sent')}
                            >
                              <Icon name="message-square" size={12} />
                              <span>WhatsApp</span>
                            </a>
                          )}

                          {f.status === 'Pending' && (
                            <button
                              type="button"
                              className="btn btn-ghost"
                              style={{ padding: '4px 6px', fontSize: 10, color: 'var(--color-success)' }}
                              onClick={() => handleUpdateStatus(f.id, 'Sent')}
                              title="Marcar como Enviado"
                            >
                              <Icon name="check" size={13} />
                            </button>
                          )}

                          {f.status === 'Pending' && (
                            <button
                              type="button"
                              className="btn btn-ghost"
                              style={{ padding: '4px 6px', fontSize: 10, color: 'var(--color-text-muted)' }}
                              onClick={() => handleUpdateStatus(f.id, 'Cancelled')}
                              title="Cancelar agendamento"
                            >
                              <Icon name="x" size={13} />
                            </button>
                          )}

                          <button
                            type="button"
                            className="btn btn-ghost"
                            style={{ padding: '4px 6px', fontSize: 10, color: 'var(--color-danger)' }}
                            onClick={() => handleDelete(f.id)}
                            title="Excluir"
                          >
                            <Icon name="trash" size={13} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal para agendar follow-up */}
      {showCreateModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0, 0, 0, 0.6)',
            display: 'grid',
            placeItems: 'center',
            zIndex: 1000,
            padding: 'var(--space-4)',
          }}
        >
          <div className="surface-card" style={{ width: '100%', maxWidth: 540, padding: 'var(--space-6)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-4)' }}>
              <h2 style={{ fontSize: 'var(--text-base)', fontWeight: 600 }}>Novo Agendamento de Follow-Up</h2>
              <button type="button" className="btn btn-ghost" onClick={() => setShowCreateModal(false)}>
                <Icon name="x" size={16} />
              </button>
            </div>

            <form onSubmit={handleCreateFollowUp} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
              <div>
                <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Cliente *</label>
                <select
                  className="input"
                  value={newCustomerId}
                  onChange={(e) => setNewCustomerId(e.target.value)}
                  style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  required
                >
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.email || c.phone || 'Sem contato'})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
                <div>
                  <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Módulo</label>
                  <select
                    className="input"
                    value={newModule}
                    onChange={(e) => setNewModule(e.target.value)}
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  >
                    <option value="sales">Vendas (Comercial)</option>
                    <option value="vet">Veterinário (Clínico)</option>
                    <option value="support">Suporte / Atendimento</option>
                  </select>
                </div>

                <div>
                  <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Canal</label>
                  <select
                    className="input"
                    value={newChannel}
                    onChange={(e) => setNewChannel(e.target.value)}
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                  >
                    <option value="WhatsApp">WhatsApp</option>
                    <option value="Email">E-mail</option>
                    <option value="SMS">SMS</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
                <div>
                  <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Tipo de Retorno</label>
                  <input
                    type="text"
                    className="input"
                    value={newType}
                    onChange={(e) => setNewType(e.target.value)}
                    placeholder="quote_reminder, foto_ferida, retorno_vacina"
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                    required
                  />
                </div>

                <div>
                  <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Data/Hora Agendada *</label>
                  <input
                    type="datetime-local"
                    className="input"
                    value={newScheduledFor}
                    onChange={(e) => setNewScheduledFor(e.target.value)}
                    style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                    required
                  />
                </div>
              </div>

              <div>
                <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Título de Referência</label>
                <input
                  type="text"
                  className="input"
                  value={newRefTitle}
                  onChange={(e) => setNewRefTitle(e.target.value)}
                  placeholder="Ex: Orçamento #1042 ou Paciente: Rex"
                  style={{ width: '100%', fontSize: 'var(--text-xs)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: 11, fontWeight: 600, display: 'block', marginBottom: 4 }}>Mensagem Sugerida</label>
                <textarea
                  className="input"
                  rows={3}
                  value={newMessageText}
                  onChange={(e) => setNewMessageText(e.target.value)}
                  placeholder="Mensagem pronta para envio via WhatsApp ou E-mail..."
                  style={{ width: '100%', fontSize: 'var(--text-xs)', resize: 'vertical' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setShowCreateModal(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? 'Agendando...' : 'Salvar Follow-Up'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AdminShell>
  );
}
