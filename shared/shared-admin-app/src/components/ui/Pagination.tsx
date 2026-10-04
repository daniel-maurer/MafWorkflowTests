import { Icon } from '../Icon';

export interface PaginationProps {
  currentPage: number;
  totalItems: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  pageSizeOptions?: number[];
}

export function Pagination({
  currentPage,
  totalItems,
  pageSize,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 25, 50, 100],
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const startItem = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(totalItems, currentPage * pageSize);

  // Calcula páginas visíveis com ellipsis
  function getVisiblePages() {
    const pages: (number | 'ellipsis')[] = [];
    if (totalPages <= 7) {
      for (let i = 1; i <= totalPages; i++) pages.push(i);
    } else {
      pages.push(1);
      if (currentPage > 3) {
        pages.push('ellipsis');
      }

      const start = Math.max(2, currentPage - 1);
      const end = Math.min(totalPages - 1, currentPage + 1);

      for (let i = start; i <= end; i++) {
        pages.push(i);
      }

      if (currentPage < totalPages - 2) {
        pages.push('ellipsis');
      }
      pages.push(totalPages);
    }
    return pages;
  }

  const visiblePages = getVisiblePages();

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: 'var(--space-3) var(--space-4)',
        borderTop: '1px solid var(--color-border)',
        backgroundColor: 'var(--color-surface)',
        fontSize: 'var(--text-xs)',
        color: 'var(--color-text-muted)',
        flexWrap: 'wrap',
        gap: 'var(--space-3)',
      }}
    >
      {/* Texto de Resumo */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
        <span>
          Mostrando <strong style={{ color: 'var(--color-text)' }}>{startItem}</strong> a{' '}
          <strong style={{ color: 'var(--color-text)' }}>{endItem}</strong> de{' '}
          <strong style={{ color: 'var(--color-text)' }}>{totalItems}</strong> produtos
        </span>

        {onPageSizeChange && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span>Exibir:</span>
            <select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              style={{
                backgroundColor: 'var(--color-surface-2)',
                color: 'var(--color-text)',
                border: '1px solid var(--color-border)',
                borderRadius: 'var(--radius-sm)',
                padding: '2px 6px',
                fontSize: '11px',
                outline: 'none',
                cursor: 'pointer',
              }}
            >
              {pageSizeOptions.map((opt) => (
                <option key={opt} value={opt}>
                  {opt} por pág.
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Controles de Navegação */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
        {/* Botão Anterior */}
        <button
          type="button"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '4px',
            padding: '4px 10px',
            borderRadius: 'var(--radius-md)',
            border: '1px solid var(--color-border)',
            backgroundColor: 'var(--color-surface-2)',
            color: currentPage <= 1 ? 'var(--color-text-faint)' : 'var(--color-text)',
            fontSize: '11px',
            fontWeight: 500,
            cursor: currentPage <= 1 ? 'not-allowed' : 'pointer',
            opacity: currentPage <= 1 ? 0.5 : 1,
            transition: 'all var(--transition)',
          }}
        >
          <Icon name="chevron-left" size={14} />
          <span>Anterior</span>
        </button>

        {/* Páginas numeradas */}
        {visiblePages.map((page, idx) => {
          if (page === 'ellipsis') {
            return (
              <span key={`ellipsis-${idx}`} style={{ padding: '0 4px', color: 'var(--color-text-faint)' }}>
                ...
              </span>
            );
          }

          const isActive = page === currentPage;
          return (
            <button
              key={page}
              type="button"
              onClick={() => onPageChange(page)}
              style={{
                minWidth: '28px',
                height: '28px',
                padding: '0 6px',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 'var(--radius-md)',
                border: `1px solid ${isActive ? 'var(--color-primary)' : 'var(--color-border)'}`,
                backgroundColor: isActive ? 'var(--color-primary)' : 'var(--color-surface-2)',
                color: isActive ? '#ffffff' : 'var(--color-text)',
                fontWeight: isActive ? 600 : 500,
                fontSize: '11px',
                cursor: 'pointer',
                transition: 'all var(--transition)',
              }}
            >
              {page}
            </button>
          );
        })}

        {/* Botão Próxima */}
        <button
          type="button"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '4px',
            padding: '4px 10px',
            borderRadius: 'var(--radius-md)',
            border: '1px solid var(--color-border)',
            backgroundColor: 'var(--color-surface-2)',
            color: currentPage >= totalPages ? 'var(--color-text-faint)' : 'var(--color-text)',
            fontSize: '11px',
            fontWeight: 500,
            cursor: currentPage >= totalPages ? 'not-allowed' : 'pointer',
            opacity: currentPage >= totalPages ? 0.5 : 1,
            transition: 'all var(--transition)',
          }}
        >
          <span>Próxima</span>
          <Icon name="chevron-right" size={14} />
        </button>
      </div>
    </div>
  );
}
