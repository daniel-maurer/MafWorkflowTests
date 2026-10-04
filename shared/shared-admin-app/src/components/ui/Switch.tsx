import React from 'react';

export interface SwitchProps {
  id?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  label?: React.ReactNode;
  description?: string;
  disabled?: boolean;
}

export function Switch({ id, checked, onChange, label, description, disabled }: SwitchProps) {
  const switchId = id || (typeof label === 'string' ? `switch-${label.toLowerCase().replace(/\s+/g, '-')}` : undefined);

  return (
    <div
      style={{
        display: 'flex',
        alignItems: description ? 'flex-start' : 'center',
        gap: '10px',
        opacity: disabled ? 0.6 : 1,
        cursor: disabled ? 'not-allowed' : 'pointer',
      }}
      onClick={() => {
        if (!disabled) onChange(!checked);
      }}
    >
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        id={switchId}
        disabled={disabled}
        onClick={(e) => {
          e.stopPropagation();
          if (!disabled) onChange(!checked);
        }}
        style={{
          width: '38px',
          height: '22px',
          borderRadius: '9999px',
          backgroundColor: checked ? 'var(--color-primary)' : 'var(--color-surface-offset)',
          border: `1px solid ${checked ? 'var(--color-primary)' : 'var(--color-border)'}`,
          position: 'relative',
          transition: 'background-color var(--transition), border-color var(--transition), box-shadow var(--transition)',
          cursor: disabled ? 'not-allowed' : 'pointer',
          padding: 0,
          flexShrink: 0,
          outline: 'none',
        }}
      >
        <span
          style={{
            position: 'absolute',
            top: '2px',
            left: checked ? '18px' : '2px',
            width: '16px',
            height: '16px',
            borderRadius: '50%',
            backgroundColor: '#ffffff',
            boxShadow: '0 1px 3px rgba(0, 0, 0, 0.3)',
            transition: 'left var(--transition)',
          }}
        />
      </button>

      {(label || description) && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          {label && (
            <label
              htmlFor={switchId}
              style={{
                fontSize: 'var(--text-xs)',
                fontWeight: 600,
                color: 'var(--color-text)',
                cursor: disabled ? 'not-allowed' : 'pointer',
              }}
            >
              {label}
            </label>
          )}
          {description && (
            <span style={{ fontSize: '11px', color: 'var(--color-text-muted)', lineHeight: 1.4 }}>
              {description}
            </span>
          )}
        </div>
      )}
    </div>
  );
}
