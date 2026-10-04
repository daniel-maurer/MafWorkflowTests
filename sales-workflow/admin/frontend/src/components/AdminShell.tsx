import React from 'react';
import { NavLink } from 'react-router-dom';
import { TopBar, Icon } from '@maf/shared-admin-app';

interface AdminShellProps {
  children: React.ReactNode;
  title?: string;
  subtitle?: string;
  action?: React.ReactNode;
}

export function AdminShell({ children, title, subtitle, action }: AdminShellProps) {
  const navItems = [
    { to: '/', label: 'Dashboard', icon: 'server' },
    { to: '/products', label: 'Produtos', icon: 'database' },
    { to: '/categories', label: 'Categorias', icon: 'tag' },
    { to: '/discounts', label: 'Descontos & Cupons', icon: 'percent' },
    { to: '/customers', label: 'Clientes (Compartilhado)', icon: 'user' },
    { to: '/follow-ups', label: 'Follow-Ups & Retornos', icon: 'message-square' },
    { to: '/payment-conditions', label: 'Condições de Pagamento', icon: 'credit-card' },
    { to: '/instructions', label: 'Instruções dos Agentes', icon: 'cpu' },
  ];

  return (
    <div className="app-shell">
      {/* 1. Toolbar Superior (100% da largura no topo da tela) */}
      <TopBar
        title="Sales Admin"
        subtitle="Painel de Gestão Comercial"
        right={
          action ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
              {action}
            </div>
          ) : undefined
        }
      />

      {/* 2. Área inferior (Painel Lateral + Conteúdo) abaixo da Toolbar */}
      <div
        style={{
          flex: 1,
          display: 'flex',
          minHeight: 0,
          overflow: 'hidden',
        }}
      >
        {/* Painel Lateral (Sidebar) */}
        <aside
          style={{
            width: 260,
            background: 'var(--color-surface)',
            borderRight: '1px solid var(--color-border)',
            display: 'flex',
            flexDirection: 'column',
            flexShrink: 0,
          }}
        >
          {/* Navegação */}
          <nav style={{ flex: 1, padding: 'var(--space-4) var(--space-3)', overflowY: 'auto' }}>
            <div
              style={{
                fontSize: 10,
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
                color: 'var(--color-text-faint)',
                padding: '0 var(--space-3) var(--space-2)',
              }}
            >
              Gerenciamento
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/'}
                  style={({ isActive }) => ({
                    display: 'flex',
                    alignItems: 'center',
                    gap: 'var(--space-3)',
                    padding: 'var(--space-2) var(--space-3)',
                    borderRadius: 'var(--radius-md)',
                    fontSize: 'var(--text-xs)',
                    fontWeight: isActive ? 600 : 500,
                    color: isActive ? 'var(--color-primary)' : 'var(--color-text-muted)',
                    background: isActive ? 'var(--color-surface-offset)' : 'transparent',
                    textDecoration: 'none',
                    transition: 'all var(--transition)',
                  })}
                >
                  <Icon name={item.icon} size={15} />
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </div>

            <div style={{ height: 1, background: 'var(--color-border)', margin: 'var(--space-4) var(--space-2)' }} />

            <div
              style={{
                fontSize: 10,
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
                color: 'var(--color-text-faint)',
                padding: '0 var(--space-3) var(--space-2)',
              }}
            >
              Workflow
            </div>
            <a
              href="http://localhost:5173/#/workflows/sales-assistant/run"
              target="_blank"
              rel="noreferrer"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 'var(--space-3)',
                padding: 'var(--space-2) var(--space-3)',
                borderRadius: 'var(--radius-md)',
                fontSize: 'var(--text-xs)',
                fontWeight: 500,
                color: 'var(--color-text-muted)',
                textDecoration: 'none',
                transition: 'all var(--transition)',
              }}
            >
              <Icon name="play" size={15} />
              <span>Executar Atendimento</span>
            </a>
          </nav>

          {/* Rodapé do painel lateral */}
          <div
            style={{
              padding: 'var(--space-3) var(--space-4)',
              borderTop: '1px solid var(--color-border)',
              fontSize: 11,
              color: 'var(--color-text-faint)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <span>pgvector: Ativo</span>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: 'var(--color-success)' }} />
          </div>
        </aside>

        {/* Área de Conteúdo */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0, overflow: 'hidden' }}>
          {(title || subtitle) && (
            <div
              style={{
                padding: 'var(--space-4) var(--space-6)',
                borderBottom: '1px solid var(--color-border)',
                background: 'var(--color-surface)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexShrink: 0,
              }}
            >
              <div>
                {title && <h1 style={{ fontSize: 'var(--text-base)', fontWeight: 600, margin: 0 }}>{title}</h1>}
                {subtitle && (
                  <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)', margin: '2px 0 0 0' }}>
                    {subtitle}
                  </p>
                )}
              </div>
            </div>
          )}

          <main
            style={{
              flex: 1,
              overflowY: 'auto',
              padding: 'var(--space-6)',
              background: 'var(--color-bg)',
            }}
          >
            <div style={{ maxWidth: 1200, margin: '0 auto' }}>{children}</div>
          </main>
        </div>
      </div>
    </div>
  );
}
