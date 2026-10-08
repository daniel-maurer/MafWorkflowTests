import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon, Card } from '@maf/shared-admin-app';
import { AdminShell } from '@/components/AdminShell';
import { StatusBadge } from '@/components/StatusBadge';
import {
  adminApi,
  type OrderItem,
  type ConversationItem,
  type CampaignItem,
  type StoreInfoItem,
} from '@/services/adminApiClient';
import './DashboardPage.css';

export function DashboardPage() {
  const navigate = useNavigate();

  const [stats, setStats] = useState({
    products: 0,
    categories: 0,
    discounts: 0,
    customers: 0,
    paymentConditions: 0,
    deliveryMethods: 0,
    campaigns: 0,
    ordersCount: 0,
    revenueTotal: 0,
    conversationsCount: 0,
    storeConfigured: false,
  });

  const [recentOrders, setRecentOrders] = useState<OrderItem[]>([]);
  const [recentConversations, setRecentConversations] = useState<ConversationItem[]>([]);
  const [activeCampaigns, setActiveCampaigns] = useState<CampaignItem[]>([]);
  const [storeInfo, setStoreInfo] = useState<StoreInfoItem | null>(null);

  const [loading, setLoading] = useState(true);
  const [reindexing, setReindexing] = useState(false);
  const [reindexMsg, setReindexMsg] = useState<string | null>(null);

  useEffect(() => {
    async function loadDashboardData() {
      setLoading(true);
      try {
        const [
          prodRes,
          catRes,
          discRes,
          custRes,
          payRes,
          delivRes,
          ordersRes,
          convRes,
          campRes,
          storeRes,
        ] = await Promise.allSettled([
          adminApi.listProducts({ pageSize: 1 }),
          adminApi.listCategories(),
          adminApi.listDiscounts({ validNow: true }),
          adminApi.listCustomers({ pageSize: 1 }),
          adminApi.listPaymentConditions(true),
          adminApi.listDeliveryMethods(),
          adminApi.listOrders(),
          adminApi.listConversations(),
          adminApi.listCampaigns(),
          adminApi.getStoreInfo(),
        ]);

        const productsTotal = prodRes.status === 'fulfilled' ? prodRes.value.total : 0;
        const categoriesTotal = catRes.status === 'fulfilled' ? catRes.value.length : 0;
        const discountsTotal = discRes.status === 'fulfilled' ? discRes.value.length : 0;
        const customersTotal = custRes.status === 'fulfilled' ? custRes.value.total : 0;
        const paymentTotal = payRes.status === 'fulfilled' ? payRes.value.length : 0;
        const deliveryTotal = delivRes.status === 'fulfilled' ? delivRes.value.length : 0;

        const ordersList = ordersRes.status === 'fulfilled' ? ordersRes.value : [];
        const revenue = ordersList.reduce((acc, curr) => acc + (curr.totalAmount || 0), 0);

        const convList = convRes.status === 'fulfilled' ? convRes.value : [];
        const campList = campRes.status === 'fulfilled' ? campRes.value : [];
        const activeCamps = campList.filter((c) => c.isActive);

        const storeData = storeRes.status === 'fulfilled' ? storeRes.value : null;

        setStats({
          products: productsTotal,
          categories: categoriesTotal,
          discounts: discountsTotal,
          customers: customersTotal,
          paymentConditions: paymentTotal,
          deliveryMethods: deliveryTotal,
          campaigns: activeCamps.length,
          ordersCount: ordersList.length,
          revenueTotal: revenue,
          conversationsCount: convList.length,
          storeConfigured: Boolean(storeData?.name && storeData?.address),
        });

        setRecentOrders(ordersList.slice(0, 5));
        setRecentConversations(convList.slice(0, 5));
        setActiveCampaigns(activeCamps.slice(0, 3));
        setStoreInfo(storeData);
      } catch (err) {
        console.error('Falha ao carregar visão geral do dashboard:', err);
      } finally {
        setLoading(false);
      }
    }

    loadDashboardData();
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
    {
      title: 'Vendas Totais',
      value: new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(stats.revenueTotal),
      sub: `${stats.ordersCount} pedido(s) gerados`,
      icon: 'dollar-sign',
      link: '/orders',
      color: 'var(--color-success)',
    },
    {
      title: 'Produtos Cadastrados',
      value: stats.products,
      sub: `${stats.categories} categorias ativas`,
      icon: 'database',
      link: '/products',
      color: 'var(--color-primary)',
    },
    {
      title: 'Clientes Unificados',
      value: stats.customers,
      sub: 'Base compartilhada de contatos',
      icon: 'user',
      link: '/customers',
      color: 'var(--color-primary)',
    },
    {
      title: 'Atendimentos de IA',
      value: stats.conversationsCount,
      sub: 'Sessões registradas no log',
      icon: 'message-square',
      link: '/conversations',
      color: 'var(--color-primary)',
    },
    {
      title: 'Campanhas Vigentes',
      value: stats.campaigns,
      sub: 'Ações promocionais ativas',
      icon: 'calendar',
      link: '/campaigns',
      color: 'var(--color-warning)',
    },
    {
      title: 'Entregas & Retiradas',
      value: stats.deliveryMethods,
      sub: 'Modalidades de frete/retirada',
      icon: 'truck',
      link: '/delivery-methods',
      color: 'var(--color-primary)',
    },
    {
      title: 'Regras de Pagamento',
      value: stats.paymentConditions,
      sub: 'Condições comerciais no QuoteAgent',
      icon: 'credit-card',
      link: '/payment-conditions',
      color: 'var(--color-primary)',
    },
    {
      title: 'Dados da Loja Física',
      value: stats.storeConfigured ? 'Configurado' : 'Pendente',
      sub: storeInfo?.name ? storeInfo.name : 'Endereço e contatos',
      icon: 'store',
      link: '/store-info',
      color: stats.storeConfigured ? 'var(--color-success)' : 'var(--color-warning)',
    },
  ];

  return (
    <AdminShell
      title="Visão Geral do E-Commerce"
      subtitle="Painel executivo de vendas, catálogo, promoções e atendimentos do Sales Workflow"
    >
      {/* Cards de Métricas Principais Grid (Size-Aware via Container Queries) */}
      <div className="dashboard-grid-container">
        <div className="stat-cards-grid">
          {statCards.map((card) => (
            <div
              key={card.title}
              onClick={() => navigate(card.link)}
              className="stat-card"
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: 'var(--space-2)',
                }}
              >
                <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-muted)' }}>
                  {card.title}
                </span>
                <div
                  style={{
                    color: card.color,
                    background: 'var(--color-surface-offset)',
                    padding: 6,
                    borderRadius: 'var(--radius-md)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <Icon name={card.icon} size={16} />
                </div>
              </div>

              <div>
                <div style={{ fontSize: 'var(--text-lg)', fontWeight: 700, color: 'var(--color-text)' }}>
                  {loading ? '...' : card.value}
                </div>
                <div style={{ fontSize: 10, color: 'var(--color-text-muted)', marginTop: 2 }}>{card.sub}</div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Seção 2: Tabelas de Atividade Recente e Operação (Responsive Tables) */}
      <div className="dashboard-section-2-col">
        {/* Últimos Pedidos Feitos */}
        <Card
          title="Últimos Pedidos Gerados pela IA"
          subtitle="Pedidos confirmados pelos clientes durante as conversas"
          action={
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => navigate('/orders')}
              style={{ fontSize: 11, padding: '4px 8px', gap: 4 }}
            >
              <span>Ver Todos</span>
              <Icon name="arrow-right" size={13} />
            </button>
          }
        >
          {loading ? (
            <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Carregando pedidos...
            </div>
          ) : recentOrders.length === 0 ? (
            <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Nenhum pedido finalizado ainda.
            </div>
          ) : (
            <div className="modern-responsive-table-wrapper">
              <table className="modern-responsive-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Data</th>
                    <th>Valor Total</th>
                    <th style={{ textAlign: 'right' }}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {recentOrders.map((o) => (
                    <tr
                      key={o.id}
                      style={{ cursor: 'pointer' }}
                      onClick={() => navigate('/orders')}
                    >
                      <td data-label="ID" style={{ fontFamily: 'monospace', fontWeight: 600, color: 'var(--color-primary)' }}>
                        #{o.id.substring(0, 8).toUpperCase()}
                      </td>
                      <td data-label="Data" style={{ color: 'var(--color-text-muted)' }}>
                        {new Date(o.createdAt).toLocaleDateString()}
                      </td>
                      <td data-label="Valor Total" style={{ fontWeight: 600 }}>
                        {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(o.totalAmount)}
                      </td>
                      <td data-label="Status" style={{ textAlign: 'right' }}>
                        <StatusBadge status={true} trueText={o.status.toUpperCase()} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        {/* Últimas Conversas de Atendimento */}
        <Card
          title="Atendimentos Recentes (Logs)"
          subtitle="Sessões ativas e concluídas com os Agentes"
          action={
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => navigate('/conversations')}
              style={{ fontSize: 11, padding: '4px 8px', gap: 4 }}
            >
              <span>Ver Logs</span>
              <Icon name="arrow-right" size={13} />
            </button>
          }
        >
          {loading ? (
            <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Carregando atendimentos...
            </div>
          ) : recentConversations.length === 0 ? (
            <div style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Nenhuma conversa registrada.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
              {recentConversations.map((c) => (
                <div
                  key={c.id}
                  onClick={() => navigate('/conversations')}
                  className="activity-item"
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                    <Icon name="message-square" size={14} />
                    <div>
                      <div style={{ fontWeight: 600, fontFamily: 'monospace' }}>
                        {c.sessionId ? c.sessionId.substring(0, 12) + '...' : c.id.substring(0, 8)}
                      </div>
                      <div style={{ fontSize: 10, color: 'var(--color-text-muted)' }}>
                        {new Date(c.createdAt).toLocaleString()}
                      </div>
                    </div>
                  </div>
                  <span
                    style={{
                      fontSize: 10,
                      fontWeight: 600,
                      padding: '2px 6px',
                      borderRadius: 4,
                      background: 'var(--color-surface-2)',
                      border: '1px solid var(--color-border)',
                    }}
                  >
                    {c.messages?.length || 0} msgs
                  </span>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>

      {/* Seção 3: RAG, Campanhas Ativas e Ações Rápidas */}
      <div className="dashboard-section-3-col">
        {/* Campanhas Ativas */}
        <Card
          title="Campanhas Vigentes"
          subtitle="Promoções e descontos progressivos ativas"
          action={
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => navigate('/campaigns')}
              style={{ fontSize: 11, padding: '4px 8px', gap: 4 }}
            >
              <span>Gerenciar</span>
              <Icon name="arrow-right" size={13} />
            </button>
          }
        >
          {activeCampaigns.length === 0 ? (
            <div style={{ padding: 'var(--space-4)', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
              Nenhuma campanha ativa no momento.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
              {activeCampaigns.map((camp) => (
                <div
                  key={camp.id}
                  className="activity-item"
                  style={{ display: 'block', padding: 'var(--space-3)' }}
                >
                  <div style={{ fontWeight: 600, color: 'var(--color-text)', marginBottom: 2 }}>
                    {camp.name}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--color-text-muted)', marginBottom: 6 }}>
                    {camp.description}
                  </div>
                  <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                    {camp.freeShipping && (
                      <span style={{ fontSize: 10, padding: '1px 6px', borderRadius: 4, background: 'var(--color-success-bg)', color: 'var(--color-success)', fontWeight: 600 }}>
                        Frete Grátis
                      </span>
                    )}
                    {camp.globalDiscountPercent > 0 && (
                      <span style={{ fontSize: 10, padding: '1px 6px', borderRadius: 4, background: 'var(--color-primary-glow)', color: 'var(--color-primary)', fontWeight: 600 }}>
                        {camp.globalDiscountPercent}% OFF
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>

        {/* RAG & Busca Semântica */}
        <Card title="RAG & Busca Semântica" subtitle="pgvector + text-embedding-3-small">
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', lineHeight: 1.5, marginBottom: 'var(--space-4)' }}>
            Todos os produtos cadastrados geram automaticamente vetores de 1536 dimensões. O CatalogAgent faz buscas semânticas em linguagem natural via cosseno.
          </p>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={handleReindex}
            disabled={reindexing}
            style={{ fontSize: 11, width: '100%', gap: 6 }}
          >
            <Icon name="sync" size={13} />
            <span>{reindexing ? 'Reindexando...' : 'Reindexar Embeddings'}</span>
          </button>
          {reindexMsg && (
            <div style={{ fontSize: 11, color: 'var(--color-success)', marginTop: 'var(--space-2)', textAlign: 'center' }}>
              {reindexMsg}
            </div>
          )}
        </Card>

        {/* Ações Rápidas */}
        <Card title="Ações Rápidas" subtitle="Atalhos para cadastro e testes">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => navigate('/products/new')}
              style={{ fontSize: 11, justifyContent: 'flex-start', gap: 8 }}
            >
              <Icon name="plus" size={14} />
              <span>Novo Produto</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/campaigns')}
              style={{ fontSize: 11, justifyContent: 'flex-start', gap: 8 }}
            >
              <Icon name="calendar" size={14} />
              <span>Nova Campanha</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/customers/new')}
              style={{ fontSize: 11, justifyContent: 'flex-start', gap: 8 }}
            >
              <Icon name="user" size={14} />
              <span>Cadastrar Cliente</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/store-info')}
              style={{ fontSize: 11, justifyContent: 'flex-start', gap: 8 }}
            >
              <Icon name="store" size={14} />
              <span>Dados da Loja</span>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/workflow/run')}
              style={{ fontSize: 11, justifyContent: 'flex-start', gap: 8 }}
            >
              <Icon name="pickaxe" size={14} />
              <span>Testar Workflow de IA</span>
            </button>
          </div>
        </Card>
      </div>
    </AdminShell>
  );
}
