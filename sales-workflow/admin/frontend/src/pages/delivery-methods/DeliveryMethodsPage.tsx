import { useEffect, useState, type FormEvent } from 'react';
import { Icon, Input, Select, Textarea, Switch, Modal } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type DeliveryMethodItem } from '@/services/adminApiClient';

export function DeliveryMethodsPage() {
  const [methods, setMethods] = useState<DeliveryMethodItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<DeliveryMethodItem | null>(null);

  const [name, setName] = useState('');
  const [type, setType] = useState('delivery');
  const [price, setPrice] = useState('0');
  const [description, setDescription] = useState('');
  const [active, setActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadMethods();
  }, []);

  async function loadMethods() {
    setLoading(true);
    try {
      const data = await adminApi.listDeliveryMethods();
      setMethods(data);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  function handleOpenCreate() {
    setEditing(null);
    setName('');
    setType('delivery');
    setPrice('0');
    setDescription('');
    setActive(true);
    setError(null);
    setShowModal(true);
  }

  function handleOpenEdit(item: DeliveryMethodItem) {
    setEditing(item);
    setName(item.name);
    setType(item.type);
    setPrice(String(item.price));
    setDescription(item.description || '');
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
      type,
      price: parseFloat(price) || 0,
      description: description.trim() || undefined,
      active,
    };

    try {
      if (editing) {
        await adminApi.updateDeliveryMethod(editing.id, payload);
      } else {
        await adminApi.createDeliveryMethod(payload);
      }
      setShowModal(false);
      loadMethods();
    } catch (err: any) {
      setError(err.message || 'Erro ao salvar método de entrega');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string, methodName: string) {
    if (!window.confirm(`Deseja remover a opção "${methodName}"?`)) return;
    try {
      await adminApi.deleteDeliveryMethod(id);
      loadMethods();
    } catch (err: any) {
      alert(err.message || 'Erro ao excluir.');
    }
  }

  return (
    <AdminShell
      title="Formas de Entrega / Retirada"
      subtitle="Modalidades de envio, entrega expressa e retirada física consultadas pelo QuoteAgent"
      action={
        <button type="button" className="btn btn-primary" onClick={handleOpenCreate} style={{ fontSize: 11, gap: 6 }}>
          <Icon name="plus" size={14} />
          <span>Nova Forma</span>
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
                <th style={{ padding: 'var(--space-3)' }}>Nome</th>
                <th style={{ padding: 'var(--space-3)' }}>Modalidade</th>
                <th style={{ padding: 'var(--space-3)' }}>Preço Fixo</th>
                <th style={{ padding: 'var(--space-3)' }}>Instruções / Descrição</th>
                <th style={{ padding: 'var(--space-3)' }}>Status</th>
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
                    Carregando formas de entrega...
                  </td>
                </tr>
              ) : methods.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Nenhuma forma de entrega ou retirada cadastrada.
                  </td>
                </tr>
              ) : (
                methods.map((m) => (
                  <tr
                    key={m.id}
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
                    <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>{m.name}</td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 4,
                          padding: '2px 8px',
                          borderRadius: 4,
                          fontSize: 10,
                          fontWeight: 600,
                          background: m.type === 'pickup' ? 'var(--color-primary-glow)' : 'var(--color-surface-offset)',
                          color: m.type === 'pickup' ? 'var(--color-primary)' : 'var(--color-text)',
                          border: '1px solid var(--color-border)',
                        }}
                      >
                        <Icon name={m.type === 'pickup' ? 'store' : 'truck'} size={12} />
                        {m.type === 'pickup' ? 'Retirada na Loja' : 'Entrega'}
                      </span>
                    </td>
                    <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>
                      {m.price === 0
                        ? 'Grátis'
                        : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(m.price)}
                    </td>
                    <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)' }}>
                      {m.description || '—'}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={m.active} />
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleOpenEdit(m)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={14} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(m.id, m.name)}
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

      {/* Modal Padronizado de Criação / Edição */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={editing ? 'Editar Forma de Entrega' : 'Nova Forma de Entrega'}
        subtitle="Defina o nome da modalidade, tipo e taxa fixa cobrada"
        icon="truck"
        maxWidth={500}
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
              form="form-delivery-method"
              className="btn btn-primary"
              disabled={saving}
              style={{ fontSize: 12, padding: '6px 18px', gap: 6 }}
            >
              <Icon name="check" size={14} />
              <span>{saving ? 'Gravando...' : 'Salvar'}</span>
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

        <form id="form-delivery-method" onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          <Input
            label="Nome da Opção"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex: Entrega Expressa via Uber Flash"
            leftIcon="truck"
            helperText="Nome apresentado ao cliente durante a cotação"
          />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Select
              label="Modalidade"
              value={type}
              onChange={(e) => setType(e.target.value)}
              leftIcon="package"
              options={[
                { value: 'delivery', label: 'Entrega (Uber / Correios / Motoboy)' },
                { value: 'pickup', label: 'Retirada na Loja Física' },
              ]}
            />

            <Input
              label="Taxa Fixa (R$)"
              type="number"
              step="0.01"
              min="0"
              required
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              leftIcon="dollar-sign"
              helperText="0 para frete grátis"
            />
          </div>

          <Textarea
            label="Instruções / Detalhes"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={2}
            placeholder="Ex: Disponível para retirada de segunda a sábado das 9h às 18h"
            helperText="Informações adicionais que o Agente repassará ao cliente"
          />

          <div style={{ paddingTop: 'var(--space-2)' }}>
            <Switch
              label="Opção Ativa"
              description="Habilita esta opção de entrega nos orçamentos"
              checked={active}
              onChange={setActive}
            />
          </div>
        </form>
      </Modal>
    </AdminShell>
  );
}
