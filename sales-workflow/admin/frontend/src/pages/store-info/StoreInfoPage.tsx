import { useEffect, useState, type FormEvent } from 'react';
import { Card, Input, Textarea, Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi, type StoreInfoItem } from '@/services/adminApiClient';

export function StoreInfoPage() {
  const [storeInfo, setStoreInfo] = useState<StoreInfoItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    loadInfo();
  }, []);

  async function loadInfo() {
    setLoading(true);
    try {
      const data = await adminApi.getStoreInfo();
      setStoreInfo(data);
    } catch (err: any) {
      console.error(err);
      setStoreInfo({ id: '', name: '', address: '', phone: '', email: '' });
    } finally {
      setLoading(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!storeInfo) return;
    setSaving(true);
    setError(null);
    setSuccess(false);

    try {
      if (storeInfo.id) {
        await adminApi.updateStoreInfo(storeInfo.id, storeInfo);
      } else {
        await adminApi.createStoreInfo(storeInfo);
      }
      setSuccess(true);
      await loadInfo();
    } catch (err: any) {
      console.error(err);
      setError(err.message || 'Erro ao salvar as informações da loja');
    } finally {
      setSaving(false);
    }
  }

  return (
    <AdminShell
      title="Dados da Loja"
      subtitle="Informações institucionais e localização consultadas pelos Agentes de IA durante o atendimento"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 720 }}>
        {error && (
          <div
            style={{
              padding: 'var(--space-3)',
              background: 'rgba(239, 68, 68, 0.1)',
              color: 'var(--color-error)',
              borderRadius: 'var(--radius-md)',
              marginBottom: 'var(--space-4)',
              fontSize: 'var(--text-xs)',
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
            }}
          >
            <Icon name="alert-circle" size={16} />
            <span>{error}</span>
          </div>
        )}

        {success && (
          <div
            style={{
              padding: 'var(--space-3)',
              background: 'rgba(34, 197, 94, 0.1)',
              color: 'var(--color-success)',
              borderRadius: 'var(--radius-md)',
              marginBottom: 'var(--space-4)',
              fontSize: 'var(--text-xs)',
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
            }}
          >
            <Icon name="check" size={16} />
            <span>Dados da loja atualizados com sucesso!</span>
          </div>
        )}

        <Card
          title="Identificação & Contato"
          subtitle="Esses dados serão fornecidos aos clientes quando perguntarem sobre a loja física ou canais de contato"
          style={{ marginBottom: 'var(--space-4)' }}
        >
          {loading ? (
            <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Carregando dados da loja...
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
              <Input
                label="Nome da Loja"
                value={storeInfo?.name || ''}
                onChange={(e) => setStoreInfo((s) => (s ? { ...s, name: e.target.value } : null))}
                required
                leftIcon="store"
                placeholder="Ex: MAF Moda Masculina & Streetwear"
                helperText="Nome apresentado pelos agentes no início ou fechamento de pedidos"
              />

              <Textarea
                label="Endereço Completo da Loja"
                value={storeInfo?.address || ''}
                onChange={(e) => setStoreInfo((s) => (s ? { ...s, address: e.target.value } : null))}
                rows={3}
                required
                placeholder="Ex: Av. Paulista, 1000 - Bela Vista, São Paulo - SP, CEP: 01310-100"
                helperText="Utilizado quando o cliente pergunta 'Onde fica a loja?' ou para cálculo de retirada presencial"
              />

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
                <Input
                  label="Telefone / WhatsApp"
                  value={storeInfo?.phone || ''}
                  onChange={(e) => setStoreInfo((s) => (s ? { ...s, phone: e.target.value } : null))}
                  leftIcon="phone"
                  placeholder="(11) 98765-4321"
                />

                <Input
                  label="E-mail de Contato"
                  type="email"
                  value={storeInfo?.email || ''}
                  onChange={(e) => setStoreInfo((s) => (s ? { ...s, email: e.target.value } : null))}
                  leftIcon="mail"
                  placeholder="contato@loja.com.br"
                />
              </div>
            </div>
          )}
        </Card>

        {/* Barra de Ações */}
        <div style={{ display: 'flex', gap: 'var(--space-3)', justifyContent: 'flex-end', paddingTop: 'var(--space-2)' }}>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={saving || loading}
            style={{ padding: '8px 24px', fontSize: 12, gap: '8px' }}
          >
            <Icon name="check" size={16} />
            <span>{saving ? 'Gravando...' : 'Salvar Informações'}</span>
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
