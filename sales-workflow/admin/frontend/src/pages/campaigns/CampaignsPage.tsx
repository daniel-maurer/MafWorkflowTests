import { useEffect, useState, type FormEvent } from 'react';
import { Icon, Input, Textarea, Switch, Modal } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import { adminApi, type CampaignItem } from '@/services/adminApiClient';

export function CampaignsPage() {
  const [campaigns, setCampaigns] = useState<CampaignItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<CampaignItem | null>(null);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [freeShipping, setFreeShipping] = useState(false);
  const [globalDiscountPercent, setGlobalDiscountPercent] = useState('0');
  const [discount1Item, setDiscount1Item] = useState('0');
  const [discount2Items, setDiscount2Items] = useState('0');
  const [discount3PlusItems, setDiscount3PlusItems] = useState('0');

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadCampaigns();
  }, []);

  async function loadCampaigns() {
    setLoading(true);
    try {
      const data = await adminApi.listCampaigns();
      setCampaigns(data);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  function handleOpenCreate() {
    setEditing(null);
    setName('');
    setDescription('');
    setStartDate(new Date().toISOString().substring(0, 16));
    setEndDate(new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().substring(0, 16));
    setIsActive(true);
    setFreeShipping(false);
    setGlobalDiscountPercent('0');
    setDiscount1Item('0');
    setDiscount2Items('0');
    setDiscount3PlusItems('0');
    setError(null);
    setShowModal(true);
  }

  function handleOpenEdit(item: CampaignItem) {
    setEditing(item);
    setName(item.name);
    setDescription(item.description);
    setStartDate(item.startDate ? item.startDate.substring(0, 16) : '');
    setEndDate(item.endDate ? item.endDate.substring(0, 16) : '');
    setIsActive(item.isActive);
    setFreeShipping(item.freeShipping);
    setGlobalDiscountPercent(String(item.globalDiscountPercent || 0));
    setDiscount1Item(String(item.discount1Item || 0));
    setDiscount2Items(String(item.discount2Items || 0));
    setDiscount3PlusItems(String(item.discount3PlusItems || 0));
    setError(null);
    setShowModal(true);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload: Partial<CampaignItem> = {
      name: name.trim(),
      description: description.trim(),
      startDate: new Date(startDate).toISOString(),
      endDate: new Date(endDate).toISOString(),
      isActive,
      freeShipping,
      globalDiscountPercent: parseFloat(globalDiscountPercent) || 0,
      discount1Item: parseFloat(discount1Item) || 0,
      discount2Items: parseFloat(discount2Items) || 0,
      discount3PlusItems: parseFloat(discount3PlusItems) || 0,
    };

    try {
      if (editing?.id) {
        await adminApi.updateCampaign(editing.id, payload);
      } else {
        await adminApi.createCampaign(payload);
      }
      setShowModal(false);
      loadCampaigns();
    } catch (err: any) {
      setError(err.message || 'Erro ao salvar campanha');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string, campName: string) {
    if (!window.confirm(`Deseja remover a campanha "${campName}"?`)) return;
    try {
      await adminApi.deleteCampaign(id);
      loadCampaigns();
    } catch (err: any) {
      alert(err.message || 'Erro ao excluir campanha.');
    }
  }

  return (
    <AdminShell
      title="Campanhas Promocionais"
      subtitle="Ações sazonais (Black Friday, Aniversário) com descontos progressivos e frete grátis aplicadas pelos Agentes"
      action={
        <button type="button" className="btn btn-primary" onClick={handleOpenCreate} style={{ fontSize: 11, gap: 6 }}>
          <Icon name="plus" size={14} />
          <span>Nova Campanha</span>
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
                <th style={{ padding: 'var(--space-3)' }}>Campanha</th>
                <th style={{ padding: 'var(--space-3)' }}>Vigência</th>
                <th style={{ padding: 'var(--space-3)' }}>Regras & Descontos</th>
                <th style={{ padding: 'var(--space-3)' }}>Instrução do Agente</th>
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
                    Carregando campanhas...
                  </td>
                </tr>
              ) : campaigns.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    style={{
                      padding: 'var(--space-8)',
                      textAlign: 'center',
                      color: 'var(--color-text-muted)',
                    }}
                  >
                    Nenhuma campanha cadastrada.
                  </td>
                </tr>
              ) : (
                campaigns.map((c) => (
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
                    <td style={{ padding: 'var(--space-3)', fontWeight: 600 }}>{c.name}</td>
                    <td style={{ padding: 'var(--space-3)', color: 'var(--color-text-muted)', whiteSpace: 'nowrap' }}>
                      {new Date(c.startDate).toLocaleDateString()} a {new Date(c.endDate).toLocaleDateString()}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                        {c.globalDiscountPercent > 0 && (
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: 10,
                              fontWeight: 600,
                              background: 'var(--color-surface-offset)',
                              border: '1px solid var(--color-border)',
                            }}
                          >
                            Global: {c.globalDiscountPercent}% OFF
                          </span>
                        )}
                        {c.discount1Item > 0 && (
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: 10,
                              fontWeight: 600,
                              background: 'var(--color-surface-offset)',
                              border: '1px solid var(--color-border)',
                            }}
                          >
                            1 un: {c.discount1Item}%
                          </span>
                        )}
                        {c.discount2Items > 0 && (
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: 10,
                              fontWeight: 600,
                              background: 'var(--color-surface-offset)',
                              border: '1px solid var(--color-border)',
                            }}
                          >
                            2 un: {c.discount2Items}%
                          </span>
                        )}
                        {c.discount3PlusItems > 0 && (
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: 10,
                              fontWeight: 600,
                              background: 'var(--color-surface-offset)',
                              border: '1px solid var(--color-border)',
                            }}
                          >
                            3+ un: {c.discount3PlusItems}%
                          </span>
                        )}
                        {c.freeShipping && (
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: 10,
                              fontWeight: 600,
                              background: 'var(--color-success-bg)',
                              color: 'var(--color-success)',
                              border: '1px solid rgba(74, 222, 128, 0.2)',
                            }}
                          >
                            📦 Frete Grátis
                          </span>
                        )}
                      </div>
                    </td>
                    <td
                      style={{
                        padding: 'var(--space-3)',
                        color: 'var(--color-text-muted)',
                        maxWidth: 240,
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}
                      title={c.description}
                    >
                      {c.description}
                    </td>
                    <td style={{ padding: 'var(--space-3)' }}>
                      <StatusBadge status={c.isActive} />
                    </td>
                    <td style={{ padding: 'var(--space-3)', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 'var(--space-1)' }}>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleOpenEdit(c)}
                          style={{ padding: '4px 8px' }}
                          title="Editar"
                        >
                          <Icon name="pen-line" size={14} />
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => handleDelete(c.id!, c.name)}
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
        title={editing ? 'Editar Campanha' : 'Nova Campanha Promocional'}
        subtitle="Configure as regras e descontos progressivos da ação"
        icon="calendar"
        maxWidth={600}
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
              form="form-campaign"
              className="btn btn-primary"
              disabled={saving}
              style={{ fontSize: 12, padding: '6px 18px', gap: 6 }}
            >
              <Icon name="check" size={14} />
              <span>{saving ? 'Gravando...' : 'Salvar Campanha'}</span>
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

        <form id="form-campaign" onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          <Input
            label="Nome da Campanha"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex: Black Friday ou Mês de Aniversário"
            leftIcon="tag"
          />

          <Textarea
            label="Como o Agente deve explicar esta campanha ao cliente"
            required
            rows={2}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Ex: Estamos em mês de aniversário! Leve 2 camisetas com 20% off e frete grátis para todo o Brasil!"
            helperText="Texto fornecido para o CatalogAgent e QuoteAgent explicarem a promoção ao cliente"
          />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
            <Input
              label="Data de Início"
              type="datetime-local"
              required
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              leftIcon="calendar"
            />

            <Input
              label="Data de Término"
              type="datetime-local"
              required
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              leftIcon="calendar"
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)', padding: 'var(--space-3)', background: 'var(--color-surface-offset)', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border)' }}>
            <Input
              label="Desconto Global no Pedido (%)"
              type="number"
              step="0.1"
              min="0"
              max="100"
              value={globalDiscountPercent}
              onChange={(e) => setGlobalDiscountPercent(e.target.value)}
              leftIcon="percent"
              helperText="Aplica sobre o subtotal independente da quantidade"
            />

            <div style={{ display: 'flex', alignItems: 'center', paddingTop: 18 }}>
              <Switch
                label="Frete Grátis na Campanha"
                description="Zera os custos de envio durante a ação"
                checked={freeShipping}
                onChange={setFreeShipping}
              />
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
            <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--color-text)' }}>Descontos Progressivos por Quantidade</span>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
              <Input
                label="1 Peça (%)"
                type="number"
                step="0.1"
                min="0"
                max="100"
                value={discount1Item}
                onChange={(e) => setDiscount1Item(e.target.value)}
                leftIcon="percent"
              />

              <Input
                label="2 Peças (%)"
                type="number"
                step="0.1"
                min="0"
                max="100"
                value={discount2Items}
                onChange={(e) => setDiscount2Items(e.target.value)}
                leftIcon="percent"
              />

              <Input
                label="3+ Peças (%)"
                type="number"
                step="0.1"
                min="0"
                max="100"
                value={discount3PlusItems}
                onChange={(e) => setDiscount3PlusItems(e.target.value)}
                leftIcon="percent"
              />
            </div>
          </div>

          <div style={{ paddingTop: 'var(--space-2)' }}>
            <Switch
              label="Campanha Ativa"
              description="Habilita esta campanha para os agentes e para aplicação nas compras"
              checked={isActive}
              onChange={setIsActive}
            />
          </div>
        </form>
      </Modal>
    </AdminShell>
  );
}
