import React, { forwardRef } from 'react';
import { Icon } from '../Icon';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  helperText?: string;
  error?: string;
  leftIcon?: string;
  rightIcon?: string;
  actionButton?: React.ReactNode;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  (
    {
      label,
      helperText,
      error,
      leftIcon,
      rightIcon,
      actionButton,
      id,
      className = '',
      style,
      disabled,
      ...props
    },
    ref
  ) => {
    const inputId = id || (label ? `input-${label.toLowerCase().replace(/\s+/g, '-')}` : undefined);

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', width: '100%' }}>
        {label && (
          <label
            htmlFor={inputId}
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

        <div
          style={{
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            width: '100%',
          }}
        >
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

          <input
            ref={ref}
            id={inputId}
            disabled={disabled}
            className={`modern-input ${className}`}
            style={{
              width: '100%',
              height: '36px',
              padding: leftIcon ? '0 12px 0 34px' : '0 12px',
              paddingRight: rightIcon || actionButton ? '36px' : '12px',
              fontSize: 'var(--text-xs)',
              color: 'var(--color-text)',
              backgroundColor: 'var(--color-surface-2)',
              border: `1px solid ${error ? 'var(--color-error)' : 'var(--color-border)'}`,
              borderRadius: 'var(--radius-md)',
              outline: 'none',
              transition: 'border-color var(--transition), box-shadow var(--transition), background-color var(--transition)',
              opacity: disabled ? 0.6 : 1,
              cursor: disabled ? 'not-allowed' : 'text',
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
          />

          {rightIcon && !actionButton && (
            <span
              style={{
                position: 'absolute',
                right: '10px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--color-text-muted)',
                pointerEvents: 'none',
              }}
            >
              <Icon name={rightIcon} size={15} />
            </span>
          )}

          {actionButton && (
            <div
              style={{
                position: 'absolute',
                right: '4px',
                display: 'flex',
                alignItems: 'center',
              }}
            >
              {actionButton}
            </div>
          )}
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

Input.displayName = 'Input';
