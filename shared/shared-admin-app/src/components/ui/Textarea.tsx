import React, { forwardRef } from 'react';
import { Icon } from '../Icon';

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  helperText?: string;
  error?: string;
  mono?: boolean;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  (
    {
      label,
      helperText,
      error,
      mono = false,
      id,
      className = '',
      style,
      disabled,
      ...props
    },
    ref
  ) => {
    const textareaId = id || (label ? `textarea-${label.toLowerCase().replace(/\s+/g, '-')}` : undefined);

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', width: '100%' }}>
        {label && (
          <label
            htmlFor={textareaId}
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

        <textarea
          ref={ref}
          id={textareaId}
          disabled={disabled}
          className={`modern-textarea ${className}`}
          style={{
            width: '100%',
            padding: '10px 12px',
            fontSize: mono ? '12px' : 'var(--text-xs)',
            fontFamily: mono ? 'var(--font-mono, monospace)' : 'inherit',
            lineHeight: 1.5,
            color: 'var(--color-text)',
            backgroundColor: 'var(--color-surface-2)',
            border: `1px solid ${error ? 'var(--color-error)' : 'var(--color-border)'}`,
            borderRadius: 'var(--radius-md)',
            outline: 'none',
            resize: 'vertical',
            minHeight: '80px',
            transition: 'border-color var(--transition), box-shadow var(--transition)',
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

Textarea.displayName = 'Textarea';
