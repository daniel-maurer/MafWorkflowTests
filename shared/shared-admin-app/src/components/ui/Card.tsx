import React from 'react';

export interface CardProps extends Omit<React.HTMLAttributes<HTMLDivElement>, 'title'> {
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  action?: React.ReactNode;
  padding?: string | number;
}

export function Card({
  title,
  subtitle,
  action,
  children,
  padding = 'var(--space-5)',
  style,
  className = '',
  ...props
}: CardProps) {
  return (
    <div
      className={`modern-card ${className}`}
      style={{
        backgroundColor: 'var(--color-surface)',
        border: '1px solid var(--color-border)',
        borderRadius: 'var(--radius-lg)',
        boxShadow: 'var(--shadow-sm)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        transition: 'border-color var(--transition), box-shadow var(--transition)',
        ...style,
      }}
      {...props}
    >
      {(title || subtitle || action) && (
        <div
          style={{
            padding: 'var(--space-4) var(--space-5)',
            borderBottom: '1px solid var(--color-border)',
            backgroundColor: 'var(--color-surface-offset)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 'var(--space-3)',
          }}
        >
          <div>
            {title && (
              <h3
                style={{
                  fontSize: 'var(--text-sm)',
                  fontWeight: 600,
                  color: 'var(--color-text)',
                  margin: 0,
                  letterSpacing: '-0.01em',
                }}
              >
                {title}
              </h3>
            )}
            {subtitle && (
              <p
                style={{
                  fontSize: '11px',
                  color: 'var(--color-text-muted)',
                  margin: '2px 0 0 0',
                }}
              >
                {subtitle}
              </p>
            )}
          </div>
          {action && <div>{action}</div>}
        </div>
      )}

      <div style={{ padding }}>{children}</div>
    </div>
  );
}

export interface FormFieldProps {
  label?: string;
  helperText?: string;
  error?: string;
  required?: boolean;
  children: React.ReactNode;
  style?: React.CSSProperties;
}

export function FormField({ label, helperText, error, required, children, style }: FormFieldProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', width: '100%', ...style }}>
      {label && (
        <label
          style={{
            fontSize: '11px',
            fontWeight: 600,
            color: 'var(--color-text)',
            display: 'flex',
            alignItems: 'center',
            gap: '4px',
            letterSpacing: '0.01em',
          }}
        >
          {label}
          {required && <span style={{ color: 'var(--color-error)' }}>*</span>}
        </label>
      )}
      {children}
      {error ? (
        <span style={{ fontSize: '11px', color: 'var(--color-error)' }}>{error}</span>
      ) : helperText ? (
        <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>{helperText}</span>
      ) : null}
    </div>
  );
}
