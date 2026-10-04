import { useEffect, useState, type FormEvent } from 'react';
import { Icon, Modal, Select, Textarea, Switch } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type AgentInstructionItem } from '@/services/adminApiClient';

const KNOWN_ROLES: { role: string; label: string; desc: string }[] = [
  { role: 'global', label: 'Global / Compartilhada', desc: 'Aplicada a todos os agentes para tom de voz e dados da loja' },
  { role: 'IntentAgent', label: 'IntentAgent (Triagem & Intenção)', desc: 'Classificação de intenções, filtros de busca e extração de produtos' },
  { role: 'CatalogAgent', label: 'CatalogAgent (Consulta de Catálogo)', desc: 'Busca semântica no estoque e apresentação de produtos' },
  { role: 'SalesAdvisorAgent', label: 'SalesAdvisorAgent (Consultor de Vendas)', desc: 'Sugestão de combos, kits promocionais e cross-sell' },
  { role: 'CustomerDecisionAgent', label: 'CustomerDecisionAgent (Avaliação de Decisão)', desc: 'Interpretação da resposta do cliente para fechamento ou novas buscas' },
  { role: 'QuoteAgent', label: 'QuoteAgent (Orçamentos & Propostas)', desc: 'Cálculo de descontos, parcelas e formalização da proposta' },
  { role: 'FollowUpAgent', label: 'FollowUpAgent (Follow-Up & Retorno)', desc: 'Agendamento de lembretes e recuperação de carrinhos' },
  { role: 'SalesRecordAgent', label: 'SalesRecordAgent (Relatório Final)', desc: 'Consolidação e registro final do histórico de atendimento' },
];

export function AgentInstructionsPage() {
  const [instructions, setInstructions] = useState<AgentInstructionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterWorkflow, setFilterWorkflow] = useState<'all' | 'global' | 'sales'>('all');
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<AgentInstructionItem | null>(null);

  // Form states
  const [workflowType, setWorkflowType] = useState('sales');
  const [agentRole, setAgentRole] = useState('CatalogAgent');
  const [instructionText, setInstructionText] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadData() {
    setLoading(true);
    try {
      const data = await adminApi.listAgentInstructions();
      setInstructions(data);
    } catch (err: any) {
      console.error('Erro ao carregar instruções:', err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  function handleOpenCreate() {
    setEditing(null);
    setWorkflowType('sales');
    setAgentRole('CatalogAgent');
    setInstructionText('');
    setIsActive(true);
    setError(null);
    setShowModal(true);
  }

  function handleOpenEdit(item: AgentInstructionItem) {
    setEditing(item);
    setWorkflowType(item.workflowType);
    setAgentRole(item.agentRole);
    setInstructionText(item.instructions);
    setIsActive(item.isActive);
    setError(null);
    setShowModal(true);
  }

  async function handleDelete(item: AgentInstructionItem) {
    if (!item.id) return;
    if (!window.confirm(`Deseja realmente excluir a instrução para o papel "${item.agentRole}"?`)) {
      return;
    }
    try {
      await adminApi.deleteAgentInstruction(item.id);
      loadData();
    } catch (err: any) {
      alert(`Falha ao excluir: ${err.message}`);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload: Partial<AgentInstructionItem> = {
      workflowType: workflowType.trim(),
      agentRole: agentRole.trim(),
      instructions: instructionText.trim(),
      isActive,
    };

    try {
      if (editing?.id) {
        await adminApi.updateAgentInstruction(editing.id, payload);
      } else {
        await adminApi.createAgentInstruction(payload);
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Erro ao salvar instrução.');
    } finally {
      setSaving(false);
    }
  }

  const filteredInstructions = instructions.filter((i) => {
    if (filterWorkflow === 'all') return true;
    return i.workflowType === filterWorkflow;
  });

  const workflowOptions = [
    { value: 'sales', label: 'Sales Workflow (Comercial)' },
    { value: 'global', label: 'Global (Compartilhado para todos os agentes)' },
    { value: 'vet', label: 'Vet Workflow' },
    { value: 'support', label: 'Support Workflow' },
  ];

  const roleOptions =
    workflowType === 'global'
      ? [{ value: 'global', label: 'global (Diretriz Transversal)' }]
      : KNOWN_ROLES.map((r) => ({ value: r.role, label: `${r.role} — ${r.label}` }));

  return (
    <AdminShell
      title="Instruções dos Agentes"
      subtitle="Gerencie os prompts do sistema, diretrizes de comportamento e tom de voz transversal"
      action={
        <button
          onClick={handleOpenCreate}
          className="btn btn-primary"
          style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', fontSize: 12, padding: '8px 16px' }}
        >
          <Icon name="plus" size={16} />
          Nova Instrução
        </button>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
        {/* Banner Informativo */}
        <div
          style={{
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: 'var(--radius-lg)',
            padding: 'var(--space-4)',
            display: 'flex',
            gap: 'var(--space-3)',
            alignItems: 'flex-start',
            boxShadow: 'var(--shadow-sm)',
          }}
        >
          <div
            style={{
              width: 36,
              height: 36,
              borderRadius: 'var(--radius-md)',
              background: 'var(--color-primary-glow)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--color-primary)',
              flexShrink: 0,
            }}
          >
            <Icon name="cpu" size={20} />
          </div>
          <div style={{ fontSize: 'var(--text-xs)', lineHeight: 1.6 }}>
            <div style={{ fontWeight: 600, color: 'var(--color-text)', marginBottom: 2 }}>
              Prompts Dinâmicos & Instruções Cadastráveis
            </div>
            <p style={{ color: 'var(--color-text-muted)', margin: 0 }}>
              Você pode alterar o comportamento e as diretrizes de qualquer agente sem precisar recompilar código.
              Instruções cadastradas com papel <strong>global</strong> são aplicadas automaticamente em todos os agentes para padronizar tom de voz e dados da loja.
            </p>
          </div>
        </div>

        {/* Filtros por Categoria */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 'var(--space-2)' }}>
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button
              onClick={() => setFilterWorkflow('all')}
              className={`btn ${filterWorkflow === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ fontSize: 11, padding: '6px 12px' }}
            >
              Todos ({instructions.length})
            </button>
            <button
              onClick={() => setFilterWorkflow('global')}
              className={`btn ${filterWorkflow === 'global' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ fontSize: 11, padding: '6px 12px' }}
            >
              Globais / Compartilhadas ({instructions.filter((i) => i.workflowType === 'global').length})
            </button>
            <button
              onClick={() => setFilterWorkflow('sales')}
              className={`btn ${filterWorkflow === 'sales' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ fontSize: 11, padding: '6px 12px' }}
            >
              Sales Workflow ({instructions.filter((i) => i.workflowType === 'sales').length})
            </button>
          </div>
        </div>

        {/* Lista de Instruções */}
        {loading ? (
          <div style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--color-text-muted)' }}>
            Carregando instruções dos agentes...
          </div>
        ) : filteredInstructions.length === 0 ? (
          <div
            style={{
              padding: 'var(--space-8)',
              textAlign: 'center',
              border: '1px dashed var(--color-border)',
              borderRadius: 'var(--radius-lg)',
              color: 'var(--color-text-muted)',
            }}
          >
            Nenhuma instrução cadastrada para este filtro.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            {filteredInstructions.map((item) => {
              const isGlobal = item.workflowType === 'global' || item.agentRole === 'global';
              const knownRole = KNOWN_ROLES.find((r) => r.role === item.agentRole);

              return (
                <div
                  key={item.id}
                  style={{
                    background: 'var(--color-surface)',
                    border: `1px solid ${isGlobal ? 'rgba(168, 85, 247, 0.4)' : 'var(--color-border)'}`,
                    borderRadius: 'var(--radius-lg)',
                    padding: 'var(--space-4)',
                    boxShadow: 'var(--shadow-sm)',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 'var(--space-3)',
                    transition: 'border-color var(--transition)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                      <span
                        style={{
                          fontSize: 'var(--text-xs)',
                          fontWeight: 700,
                          padding: '3px 10px',
                          borderRadius: 'var(--radius-sm)',
                          background: isGlobal ? 'rgba(168, 85, 247, 0.15)' : 'var(--color-surface-offset)',
                          color: isGlobal ? '#c084fc' : 'var(--color-text)',
                        }}
                      >
                        {item.agentRole}
                      </span>
                      <span
                        style={{
                          fontSize: 11,
                          padding: '2px 8px',
                          borderRadius: 4,
                          background: 'rgba(59, 130, 246, 0.1)',
                          color: '#60a5fa',
                          textTransform: 'uppercase',
                          fontWeight: 600,
                        }}
                      >
                        {item.workflowType}
                      </span>
                      {knownRole && (
                        <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
                          • {knownRole.label}
                        </span>
                      )}
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                      <StatusBadge status={item.isActive} />
                      <button
                        type="button"
                        onClick={() => handleOpenEdit(item)}
                        className="btn btn-secondary"
                        style={{ fontSize: 11, padding: '4px 10px', gap: '4px' }}
                        title="Editar instrução no modal"
                      >
                        <Icon name="pen-line" size={13} />
                        Editar Instrução
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(item)}
                        className="btn btn-ghost"
                        style={{ padding: '4px 8px', color: 'var(--color-error)' }}
                        title="Excluir instrução"
                      >
                        <Icon name="trash-2" size={14} />
                      </button>
                    </div>
                  </div>

                  {knownRole && (
                    <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>
                      {knownRole.desc}
                    </div>
                  )}

                  {/* Prévia do Prompt */}
                  <div
                    onClick={() => handleOpenEdit(item)}
                    style={{
                      background: 'var(--color-bg)',
                      border: '1px solid var(--color-border)',
                      borderRadius: 'var(--radius-md)',
                      padding: 'var(--space-3)',
                      fontSize: 'var(--text-xs)',
                      fontFamily: 'var(--font-mono, monospace)',
                      whiteSpace: 'pre-wrap',
                      maxHeight: 140,
                      overflowY: 'auto',
                      color: 'var(--color-text)',
                      lineHeight: 1.5,
                      cursor: 'pointer',
                      transition: 'border-color var(--transition)',
                    }}
                    title="Clique para editar"
                    onMouseEnter={(e) => {
                      e.currentTarget.style.borderColor = 'var(--color-primary)';
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.borderColor = 'var(--color-border)';
                    }}
                  >
                    {item.instructions}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* MODAL MODERNO DE EDIÇÃO / CRIAÇÃO */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={editing ? `Editar Diretrizes: ${agentRole}` : 'Nova Instrução de Agente'}
        subtitle={
          editing
            ? `Atualize o comportamento em tempo real do agente (${agentRole})`
            : 'Cadastre um novo prompt do sistema'
        }
        icon="cpu"
        maxWidth={720}
        footer={
          <>
            <button
              type="button"
              onClick={() => setShowModal(false)}
              className="btn btn-secondary"
              style={{ padding: '8px 16px', fontSize: 12 }}
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={saving}
              onClick={handleSubmit}
              className="btn btn-primary"
              style={{ padding: '8px 20px', fontSize: 12, gap: '6px' }}
            >
              <Icon name="check" size={15} />
              <span>{saving ? 'Gravando...' : editing ? 'Salvar Alterações' : 'Criar Instrução'}</span>
            </button>
          </>
        }
      >
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          {error && (
            <div
              style={{
                padding: 'var(--space-3) var(--space-4)',
                background: 'var(--color-error-bg)',
                color: 'var(--color-error)',
                borderRadius: 'var(--radius-md)',
                fontSize: 'var(--text-xs)',
                display: 'flex',
                alignItems: 'center',
                gap: 'var(--space-2)',
              }}
            >
              <Icon name="alert-circle" size={15} />
              <span>{error}</span>
            </div>
          )}

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Select
              label="Workflow Alvo"
              options={workflowOptions}
              value={workflowType}
              onChange={(e) => {
                const val = e.target.value;
                setWorkflowType(val);
                if (val === 'global') setAgentRole('global');
              }}
              leftIcon="layers"
            />

            <Select
              label="Papel do Agente (Agent Role)"
              options={roleOptions}
              value={agentRole}
              onChange={(e) => setAgentRole(e.target.value)}
              leftIcon="cpu"
              disabled={workflowType === 'global'}
            />
          </div>

          <Textarea
            label="Texto do Prompt / Instruções do Sistema"
            mono
            rows={12}
            value={instructionText}
            onChange={(e) => setInstructionText(e.target.value)}
            placeholder="Digite as instruções e diretrizes comportamentais para o agente..."
            helperText="Suporta regras, restrições, tom de voz e formato estrito JSON."
            required
          />

          <Switch
            checked={isActive}
            onChange={(checked) => setIsActive(checked)}
            label="Instrução Ativa"
            description="Quando desmarcada, os workers usarão o prompt padrão pré-compilado de fallback"
          />
        </form>
      </Modal>
    </AdminShell>
  );
}
