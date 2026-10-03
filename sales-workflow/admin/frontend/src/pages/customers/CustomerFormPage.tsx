import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
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
      adminApi.getCustomer(id)
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

  function handleAddAddress() {
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
    setAddresses([...addresses, newAddr]);
    setNewStreet('');
    setNewNumber('');
    setNewNeighborhood('');
    setNewCity('');
    setNewState('');
    setNewZipCode('');
    setShowAddressForm(false);
  }

  function handleRemoveAddress(index: number) {
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
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <AdminShell
      title={isEditing ? `Editar Cliente: ${name}` : 'Cadastrar Novo Cliente'}
      subtitle="Base de clientes centralizada — reutilizada em todos os módulos e workflows"
    >
      <form onSubmit={handleSubmit} style={{ maxWidth: 800 }}>
        {error && (
          <div style={{ padding: 'var(--space-3)', background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', borderRadius: 'var(--radius-md)', marginBottom: 'var(--space-4)', fontSize: 'var(--text-xs)' }}>
            {error}
          </div>
        )}

        <div className="surface-card" style={{ padding: 'var(--space-5)', marginBottom: 'var(--space-5)' }}>
          <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-4)' }}>Dados Cadastrais</h3>

          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Nome Completo / Razão Social *</label>
              <input
                type="text"
                className="input"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ex: João da Silva ou Empresa LTDA"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Tipo de Cliente</label>
              <select
                className="input"
                value={customerType}
                onChange={(e) => setCustomerType(e.target.value as any)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              >
                <option value="individual">Pessoa Física</option>
                <option value="business">Pessoa Jurídica</option>
              </select>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>E-mail *</label>
              <input
                type="email"
                className="input"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="cliente@email.com"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Telefone / WhatsApp</label>
              <input
                type="text"
                className="input"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="(11) 99999-9999"
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Tipo de Documento</label>
              <select
                className="input"
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value)}
                style={{ width: '100%', fontSize: 'var(--text-xs)' }}
              >
                <option value="cpf">CPF</option>
                <option value="cnpj">CNPJ</option>
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Número do Documento</label>
              <input
                type="text"
                className="input"
                value={documentNumber}
                onChange={(e) => setDocumentNumber(e.target.value)}
                placeholder="000.000.000-00"
                style={{ width: '100%', fontSize: 'var(--text-xs)', fontFamily: 'monospace' }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Observações / Histórico</label>
            <textarea
              className="input"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Preferências de entrega, condições negociadas, etc."
              style={{ width: '100%', fontSize: 'var(--text-xs)' }}
            />
          </div>
        </div>

        {/* Endereços */}
        <div className="surface-card" style={{ padding: 'var(--space-5)', marginBottom: 'var(--space-5)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-3)' }}>
            <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600 }}>Endereços de Entrega & Cobrança</h3>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setShowAddressForm(!showAddressForm)}
              style={{ fontSize: 11 }}
            >
              <Icon name="plus" size={13} />
              <span>{showAddressForm ? 'Fechar' : 'Adicionar Endereço'}</span>
            </button>
          </div>

          {addresses.length === 0 ? (
            <p style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Nenhum endereço cadastrado para este cliente.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', marginBottom: 'var(--space-3)' }}>
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
                  <div>
                    <span style={{ fontWeight: 600, marginRight: 8 }}>[{a.label}]</span>
                    {a.street}, {a.number} {a.complement ? `(${a.complement})` : ''} - {a.neighborhood}, {a.city}/{a.state} - CEP: {a.zipCode}
                    {a.isDefault && <span style={{ marginLeft: 8, fontSize: 10, color: 'var(--color-success)', fontWeight: 600 }}>(Padrão)</span>}
                  </div>
                  <button
                    type="button"
                    className="btn btn-ghost"
                    onClick={() => handleRemoveAddress(idx)}
                    style={{ padding: '2px 6px', color: 'var(--color-danger)' }}
                  >
                    <Icon name="x" size={13} />
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Form inline para adicionar endereço */}
          {showAddressForm && (
            <div style={{ padding: 'var(--space-4)', background: 'var(--color-surface-offset)', borderRadius: 'var(--radius-md)', marginTop: 'var(--space-3)' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 3fr 1fr', gap: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>Rótulo</label>
                  <input type="text" className="input" value={newLabel} onChange={(e) => setNewLabel(e.target.value)} placeholder="Principal, Casa, etc" style={{ width: '100%', fontSize: 11 }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>Logradouro / Rua</label>
                  <input type="text" className="input" value={newStreet} onChange={(e) => setNewStreet(e.target.value)} placeholder="Av. Paulista" style={{ width: '100%', fontSize: 11 }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>Número</label>
                  <input type="text" className="input" value={newNumber} onChange={(e) => setNewNumber(e.target.value)} placeholder="1000" style={{ width: '100%', fontSize: 11 }} />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '2fr 2fr 1fr 1fr', gap: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>Bairro</label>
                  <input type="text" className="input" value={newNeighborhood} onChange={(e) => setNewNeighborhood(e.target.value)} placeholder="Bela Vista" style={{ width: '100%', fontSize: 11 }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>Cidade</label>
                  <input type="text" className="input" value={newCity} onChange={(e) => setNewCity(e.target.value)} placeholder="São Paulo" style={{ width: '100%', fontSize: 11 }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>UF</label>
                  <input type="text" className="input" maxLength={2} value={newState} onChange={(e) => setNewState(e.target.value)} placeholder="SP" style={{ width: '100%', fontSize: 11 }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 10, fontWeight: 600, marginBottom: 2 }}>CEP</label>
                  <input type="text" className="input" value={newZipCode} onChange={(e) => setNewZipCode(e.target.value)} placeholder="01310-100" style={{ width: '100%', fontSize: 11 }} />
                </div>
              </div>
              <button type="button" className="btn btn-primary" onClick={handleAddAddress} style={{ fontSize: 11 }}>
                Incluir Endereço
              </button>
            </div>
          )}
        </div>

        <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
          <button type="submit" className="btn btn-primary" disabled={saving}>
            <Icon name="check" size={15} />
            <span>{saving ? 'Gravando...' : isEditing ? 'Salvar Alterações' : 'Cadastrar Cliente'}</span>
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => navigate('/customers')}>
            Cancelar
          </button>
        </div>
      </form>
    </AdminShell>
  );
}
