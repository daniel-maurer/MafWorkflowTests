import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { adminApi } from '@/services/adminApiClient';

export function DashboardPage() {
  const navigate = useNavigate();
  const [stats, setStats] = useState({
    products: 0,
    categories: 0,
    discounts: 0,
    customers: 0,
    paymentConditions: 0,
  });
  const [loading, setLoading] = useState(true);
  const [reindexing, setReindexing] = useState(false);
  const [reindexMsg, setReindexMsg] = useState<string | null>(null);

  useEffect(() => {
    async function loadStats() {
      try {
        const [prod, cat, disc, cust, pay] = await Promise.all([
          adminApi.listProducts({ pageSize: 1 }),
          adminApi.listCategories(),
          adminApi.listDiscounts({ validNow: true }),
          adminApi.listCustomers({ pageSize: 1 }),
          adminApi.listPaymentConditions(true),
        ]);
        setStats({
          products: prod.total,
          categories: cat.length,
          discounts: disc.length,
          customers: cust.total,
          paymentConditions: pay.length,
        });
      } catch (err) {
        console.error('Falha ao carregar métricas:', err);
      } finally {
        setLoading(false);
      }
    }
    loadStats();
  }, []);

  async function handleReindex() {
    setReindexing(true);
    setReindexMsg(null);
    try {
      const res = await adminApi.reindexEmbeddings();
      setReindexMsg(res.message);
    } catch (err: any) {
      setReindexMsg(`Erro: ${err.message}`);
    } finally {
      setReindexing(false);
    }
  }

  const statCards = [
    { title: 'Produtos no Catálogo', value: stats.products, icon: 'database', link: '/products', color: 'var(--color-primary)' },
    { title: 'Categorias Ativas', value: stats.categories, icon: 'tag', link: '/categories', color: 'var(--color-primary)' },
    { title: 'Descontos & Cupons', value: stats.discounts, icon: 'percent', link: '/discounts', color: 'var(--color-warning)' },
    { title: 'Clientes Cadastrados', value: stats.customers, icon: 'user', link: '/customers', color: 'var(--color-success)' },
    { title: 'Condições de Pagamento', value: stats.paymentConditions, icon: 'credit-card', link: '/payment-conditions', color: 'var(--color-primary)' },
  ];

  return (
    <AdminShell title="Visão Geral do E-Commerce" subtitle="Painel de controle de catálogo, promoções e clientes para o Sales Workflow">
      {/* Metric Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-4)', marginBottom: 'var(--space-6)' }}>
        {statCards.map((card) => (
          <div
            key={card.title}
            onClick={() => navigate(card.link)}
            className="surface-card"
            style={{
              padding: 'var(--space-4)',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
              transition: 'transform var(--transition), box-shadow var(--transition)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-2)' }}>
              <span style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)' }}>{card.title}</span>
              <div style={{ color: card.color, background: 'var(--color-surface-offset)', padding: 6, borderRadius: 'var(--radius-md)' }}>
                <Icon name={card.icon} size={16} />
              </div>
            </div>
            <div style={{ fontSize: 'var(--text-xl)', fontWeight: 700 }}>
              {loading ? '...' : card.value}
            </div>
          </div>
        ))}
      </div>

      {/* RAG and Quick Actions section */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-5)' }}>
        {/* RAG Status */}
        <div className="surface-card" style={{ padding: 'var(--space-5)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
            <div style={{ background: 'var(--color-surface-offset)', padding: 8, borderRadius: 'var(--radius-md)', color: 'var(--color-primary)' }}>
              <Icon name="search" size={18} />
            </div>
            <div>
              <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600 }}>RAG & Busca Semântica (pgvector)</h3>
              <p style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>PostgreSQL com pgvector e embeddings text-embedding-3-small</p>
            </div>
          </div>
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', lineHeight: 1.5, marginBottom: 'var(--space-4)' }}>
            Todos os produtos cadastrados geram automaticamente um vetor de 1536 dimensões no PostgreSQL. O agente comercial utiliza a ferramenta de busca para localizar produtos por linguagem natural com cálculo de distância por cosseno.
          </p>
          <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'center' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleReindex}
              disabled={reindexing}
              style={{ fontSize: 11 }}
            >
              <Icon name="sync" size={13} />
              <span>{reindexing ? 'Reindexando...' : 'Reindexar Embeddings'}</span>
            </button>
            {reindexMsg && <span style={{ fontSize: 11, color: 'var(--color-success)' }}>{reindexMsg}</span>}
          </div>
        </div>

        {/* Quick Actions */}
        <div className="surface-card" style={{ padding: 'var(--space-5)' }}>
          <h3 style={{ fontSize: 'var(--text-sm)', fontWeight: 600, marginBottom: 'var(--space-3)' }}>Ações Rápidas</h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => navigate('/products/new')}
              style={{ fontSize: 11, justifyContent: 'flex-start' }}
            >
              <Icon name="plus" size={14} />
              <span>Novo Produto</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/discounts/new')}
              style={{ fontSize: 11, justifyContent: 'flex-start' }}
            >
              <Icon name="percent" size={14} />
              <span>Novo Cupom</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/customers/new')}
              style={{ fontSize: 11, justifyContent: 'flex-start' }}
            >
              <Icon name="user" size={14} />
              <span>Cadastrar Cliente</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/workflow/run')}
              style={{ fontSize: 11, justifyContent: 'flex-start' }}
            >
              <Icon name="pickaxe" size={14} />
              <span>Testar Workflow</span>
            </button>
          </div>
        </div>
      </div>
    </AdminShell>
  );
}
