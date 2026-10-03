interface StatusBadgeProps {
  status: boolean | string;
  trueText?: string;
  falseText?: string;
}

export function StatusBadge({ status, trueText = 'Ativo', falseText = 'Inativo' }: StatusBadgeProps) {
  const isOk = typeof status === 'boolean' ? status : status === 'active' || status === 'valid';

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 5,
        fontSize: 11,
        fontWeight: 600,
        padding: '2px 8px',
        borderRadius: 'var(--radius-full)',
        background: isOk ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
        color: isOk ? 'var(--color-success)' : 'var(--color-danger)',
        border: `1px solid ${isOk ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)'}`,
      }}
    >
      <span
        style={{
          width: 6,
          height: 6,
          borderRadius: '50%',
          background: isOk ? 'var(--color-success)' : 'var(--color-danger)',
        }}
      />
      {isOk ? trueText : falseText}
    </span>
  );
}
