import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Icon, Input, Select, Textarea, Switch, Card } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi, type CustomerAddressItem } from '@/services/adminApiClient';

export function CustomerFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEditing = Boolean(id);
  const navigate = useNavigate();

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [documentType, setDocumentType] = useState('cpf');
  const [customerType, setCustomerType] = useState<'individual' | 'business'>('individual');
  const [notes, setNotes] = useState('');
  const [active, setActive] = useState(true);

  const [addresses, setAddresses] = useState<CustomerAddressItem[]>([]);
  const [newLabel, setNewLabel] = useState('Principal');
  const [newStreet, setNewStreet] = useState('');
  const [newNumber, setNewNumber] = useState('');
  const [newNeighborhood, setNewNeighborhood] = useState('');
  const [newCity, setNewCity] = useState('');
  const [newState, setNewState] = useState('');
  const [newZipCode, setNewZipCode] = useState('');
  const [showAddressForm, setShowAddressForm] = useState(false);

  useEffect(() => {
    if (id) {
      adminApi
        .getCustomer(id)
        .then((c) => {
          setName(c.name);
          setEmail(c.email);
          setPhone(c.phone || '');
          setDocumentNumber(c.documentNumber || '');
          setDocumentType(c.documentType || 'cpf');
          setCustomerType(c.customerType);
          setNotes(c.notes || '');
          setActive(c.active);
          setAddresses(c.addresses || []);
        })
        .catch((err) => setError(err.message));
    }
  }, [id]);

  async function handleAddAddress() {
    if (!newStreet || !newCity) {
      alert('Informe ao menos logradouro e cidade.');
      return;
    }
    const newAddr: CustomerAddressItem = {
      label: newLabel,
      street: newStreet,
      number: newNumber,
      neighborhood: newNeighborhood,
      city: newCity,
      state: newState.toUpperCase(),
      zipCode: newZipCode,
      isDefault: addresses.length === 0,
    };

    if (isEditing && id) {
      try {
        const savedAddr = await adminApi.addCustomerAddress(id, newAddr);
        setAddresses([...addresses, savedAddr]);
      } catch (e) {
        console.error(e);
        alert('Erro ao adicionar endereço');
        return;
      }
    } else {
      setAddresses([...addresses, newAddr]);
    }

    setNewStreet('');
    setNewNumber('');
    setNewNeighborhood('');
    setNewCity('');
    setNewState('');
    setNewZipCode('');
    setShowAddressForm(false);
  }

  async function handleRemoveAddress(index: number) {
    const addr = addresses[index];
    if (isEditing && id && addr.id) {
      if (!confirm('Excluir endereço definitivamente?')) return;
      try {
        await adminApi.deleteCustomerAddress(id, addr.id);
      } catch (e) {
        console.error(e);
        alert('Erro ao remover endereço');
        return;
      }
    }
    setAddresses(addresses.filter((_, i) => i !== index));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload = {
      name: name.trim(),
      email: email.trim().toLowerCase(),
      phone: phone.trim() || undefined,
      documentNumber: documentNumber.trim() || undefined,
      documentType,
      customerType,
      notes: notes.trim() || undefined,
      active,
      addresses,
    };

    try {
      if (isEditing && id) {
        await adminApi.updateCustomer(id, payload);
      } else {
        await adminApi.createCustomer(payload);
      }
      navigate('/customers');
    } catch (err: any) {
      setError(err.message || 'Erro ao gravar cliente');
    } finally {
      setSaving(false);
    }
  }

  return (
    <AdminShell
      title={isEditing ? `Editar Cliente: ${name}` : 'Cadastrar Novo Cliente'}
      subtitle="Base de clientes centralizada — reutilizada em todos os módulos e workflows"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 840 }}>
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

        {/* Card 1: Dados Cadastrais */}
        <Card
          title="Dados Cadastrais"
          subtitle="Identificação, tipo de cliente e canais de contato"
          style={{ marginBottom: 'var(--space-5)' }}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 'var(--space-4)' }}>
              <Input
                label="Nome Completo / Razão Social"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ex: João da Silva ou Empresa LTDA"
                leftIcon="user"
              />

              <Select
                label="Tipo de Cliente"
                value={customerType}
                onChange={(e) => setCustomerType(e.target.value as any)}
                leftIcon="briefcase"
                options={[
                  { value: 'individual', label: 'Pessoa Física' },
                  { value: 'business', label: 'Pessoa Jurídica' },
                ]}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
              <Input
                label="E-mail"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="cliente@email.com"
                leftIcon="mail"
              />

              <Input
                label="Telefone / WhatsApp"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="(11) 99999-9999"
                leftIcon="phone"
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 'var(--space-4)' }}>
              <Select
                label="Tipo de Documento"
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value)}
                leftIcon="file-text"
                options={[
                  { value: 'cpf', label: 'CPF' },
                  { value: 'cnpj', label: 'CNPJ' },
                ]}
              />

              <Input
                label="Número do Documento"
                value={documentNumber}
                onChange={(e) => setDocumentNumber(e.target.value)}
                placeholder="000.000.000-00"
                leftIcon="hash"
              />
            </div>

            <Textarea
              label="Observações / Highlights das Conversas"
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Preferências de compra, estilo, tamanhos de interesse ou anotações extraídas pelos Agentes..."
              helperText="Informações utilizadas pelos Agentes para personalizar as próximas conversas"
            />

            <div style={{ paddingTop: 'var(--space-1)' }}>
              <Switch
                label="Cliente Ativo"
                description="Permite que o cliente seja localizado e atenda pedidos"
                checked={active}
                onChange={setActive}
              />
            </div>
          </div>
        </Card>

        {/* Card 2: Endereços */}
        <Card
          title="Endereços de Entrega & Cobrança"
          subtitle="Locais salvos para envio de compras e cálculo de entrega pelos Agentes"
          action={
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setShowAddressForm(!showAddressForm)}
              style={{ fontSize: 11, padding: '4px 12px', gap: 6 }}
            >
              <Icon name={showAddressForm ? 'x' : 'plus'} size={13} />
              <span>{showAddressForm ? 'Cancelar' : 'Adicionar Endereço'}</span>
            </button>
          }
          style={{ marginBottom: 'var(--space-5)' }}
        >
          {addresses.length === 0 ? (
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', margin: 0 }}>
              Nenhum endereço cadastrado para este cliente.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', marginBottom: showAddressForm ? 'var(--space-4)' : 0 }}>
              {addresses.map((a, idx) => (
                <div
                  key={idx}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: 'var(--space-3)',
                    background: 'var(--color-surface-offset)',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--color-border)',
                    fontSize: 'var(--text-xs)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                    <Icon name="map-pin" size={14} />
                    <div>
                      <span style={{ fontWeight: 600, marginRight: 6 }}>[{a.label}]</span>
                      {a.street}, {a.number} {a.complement ? `(${a.complement})` : ''} - {a.neighborhood}, {a.city}/{a.state} - CEP: {a.zipCode}
                      {a.isDefault && (
                        <span style={{ marginLeft: 8, fontSize: 10, color: 'var(--color-success)', fontWeight: 600 }}>
                          (Padrão)
                        </span>
                      )}
                    </div>
                  </div>
                  <button
                    type="button"
                    className="btn btn-ghost"
                    onClick={() => handleRemoveAddress(idx)}
                    style={{ padding: '4px 8px', color: 'var(--color-error)' }}
                    title="Excluir Endereço"
                  >
                    <Icon name="x" size={14} />
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Form inline para adicionar endereço */}
          {showAddressForm && (
            <div
              style={{
                padding: 'var(--space-4)',
                background: 'var(--color-surface-offset)',
                borderRadius: 'var(--radius-md)',
                border: '1px solid var(--color-border)',
                marginTop: 'var(--space-3)',
                display: 'flex',
                flexDirection: 'column',
                gap: 'var(--space-3)',
              }}
            >
              <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--color-text)' }}>Novo Endereço</span>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 3fr 1fr', gap: 'var(--space-3)' }}>
                <Input
                  label="Rótulo"
                  value={newLabel}
                  onChange={(e) => setNewLabel(e.target.value)}
                  placeholder="Principal, Casa, Trabalho"
                  leftIcon="tag"
                />
                <Input
                  label="Logradouro / Rua"
                  value={newStreet}
                  onChange={(e) => setNewStreet(e.target.value)}
                  placeholder="Av. Paulista"
                  leftIcon="map-pin"
                />
                <Input
                  label="Número"
                  value={newNumber}
                  onChange={(e) => setNewNumber(e.target.value)}
                  placeholder="1000"
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 2fr 1fr 1fr', gap: 'var(--space-3)' }}>
                <Input
                  label="Bairro"
                  value={newNeighborhood}
                  onChange={(e) => setNewNeighborhood(e.target.value)}
                  placeholder="Bela Vista"
                />
                <Input
                  label="Cidade"
                  value={newCity}
                  onChange={(e) => setNewCity(e.target.value)}
                  placeholder="São Paulo"
                />
                <Input
                  label="UF"
                  maxLength={2}
                  value={newState}
                  onChange={(e) => setNewState(e.target.value)}
                  placeholder="SP"
                />
                <Input
                  label="CEP"
                  value={newZipCode}
                  onChange={(e) => setNewZipCode(e.target.value)}
                  placeholder="01310-100"
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-2)', paddingTop: 'var(--space-1)' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setShowAddressForm(false)}
                  style={{ fontSize: 11, padding: '6px 14px' }}
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={handleAddAddress}
                  style={{ fontSize: 11, padding: '6px 16px', gap: 6 }}
                >
                  <Icon name="check" size={13} />
                  <span>Salvar Endereço</span>
                </button>
              </div>
            </div>
          )}
        </Card>

        {/* Barra de Ações */}
        <div style={{ display: 'flex', gap: 'var(--space-3)', justifyContent: 'flex-end', paddingTop: 'var(--space-2)' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => navigate('/customers')}
            style={{ padding: '8px 20px', fontSize: 12 }}
          >
            Cancelar
          </button>

          <button
            type="submit"
            className="btn btn-primary"
            disabled={saving}
            style={{ padding: '8px 24px', fontSize: 12, gap: '8px' }}
          >
            <Icon name="check" size={16} />
            <span>{saving ? 'Gravando...' : isEditing ? 'Salvar Alterações' : 'Cadastrar Cliente'}</span>
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
