import React, { forwardRef } from 'react';
import { Icon } from '../Icon';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  helperText?: string;
  error?: string;
  leftIcon?: string;
  options?: SelectOption[];
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  (
    {
      label,
      helperText,
      error,
      leftIcon,
      options,
      children,
      id,
      className = '',
      style,
      disabled,
      ...props
    },
    ref
  ) => {
    const selectId = id || (label ? `select-${label.toLowerCase().replace(/\s+/g, '-')}` : undefined);

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', width: '100%' }}>
        {label && (
          <label
            htmlFor={selectId}
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
            {props.required && <span style={{ color: 'var(--color-error)' }}>*</span>}
          </label>
        )}

        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', width: '100%' }}>
          {leftIcon && (
            <span
              style={{
                position: 'absolute',
                left: '10px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--color-text-muted)',
                pointerEvents: 'none',
                zIndex: 1,
              }}
            >
              <Icon name={leftIcon} size={15} />
            </span>
          )}

          <select
            ref={ref}
            id={selectId}
            disabled={disabled}
            className={`modern-select ${className}`}
            style={{
              width: '100%',
              height: '36px',
              appearance: 'none',
              WebkitAppearance: 'none',
              padding: leftIcon ? '0 32px 0 34px' : '0 32px 0 12px',
              fontSize: 'var(--text-xs)',
              color: 'var(--color-text)',
              backgroundColor: 'var(--color-surface-2)',
              border: `1px solid ${error ? 'var(--color-error)' : 'var(--color-border)'}`,
              borderRadius: 'var(--radius-md)',
              outline: 'none',
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.6 : 1,
              transition: 'border-color var(--transition), box-shadow var(--transition)',
              ...style,
            }}
            onFocus={(e) => {
              e.currentTarget.style.borderColor = error ? 'var(--color-error)' : 'var(--color-primary)';
              e.currentTarget.style.boxShadow = `0 0 0 3px ${error ? 'var(--color-error-bg)' : 'var(--color-primary-glow)'}`;
              props.onFocus?.(e);
            }}
            onBlur={(e) => {
              e.currentTarget.style.borderColor = error ? 'var(--color-error)' : 'var(--color-border)';
              e.currentTarget.style.boxShadow = 'none';
              props.onBlur?.(e);
            }}
            {...props}
          >
            {options
              ? options.map((opt) => (
                  <option
                    key={opt.value}
                    value={opt.value}
                    disabled={opt.disabled}
                    style={{
                      backgroundColor: 'var(--color-surface)',
                      color: 'var(--color-text)',
                      padding: '8px',
                    }}
                  >
                    {opt.label}
                  </option>
                ))
              : children}
          </select>

          {/* Custom Modern Chevron Arrow */}
          <span
            style={{
              position: 'absolute',
              right: '10px',
              pointerEvents: 'none',
              color: 'var(--color-text-muted)',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <Icon name="chevron-down" size={14} />
          </span>
        </div>

        {error ? (
          <span style={{ fontSize: '11px', color: 'var(--color-error)', display: 'flex', alignItems: 'center', gap: '4px' }}>
            <Icon name="alert-circle" size={12} />
            {error}
          </span>
        ) : helperText ? (
          <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>{helperText}</span>
        ) : null}
      </div>
    );
  }
);

Select.displayName = 'Select';
