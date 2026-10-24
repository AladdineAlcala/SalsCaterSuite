import type { ReactNode } from 'react'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import { cn } from '@/utils/cn'

const CONTROL_CLASS =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

const INVALID_CONTROL_CLASS = 'border-error focus:border-error focus:ring-error/15'

export interface WorkbenchFieldShellProps {
  id: string
  label: string
  /** The validation message — it is tied to the control, never floating. */
  error?: string
  /** A quiet hint under the control. */
  hint?: string
  className?: string
  children: ReactNode
}

/**
 * The shell every workbench control sits in.
 *
 * The label is **explicit** and the validation message is tied to the control
 * through `aria-describedby`, so a screen reader reads the label, the value
 * and the reason together (spec §5.5).
 */
export function WorkbenchFieldShell({
  id,
  label,
  error,
  hint,
  className,
  children,
}: WorkbenchFieldShellProps) {
  return (
    <div className={cn('flex flex-col gap-1 min-w-0', className)}>
      <label
        htmlFor={id}
        className="font-caption text-caption text-espresso-400 font-semibold uppercase"
      >
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${id}-error`} role="alert" className="font-caption text-caption text-error">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="font-caption text-caption text-espresso-400">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

export interface WorkbenchTextFieldProps {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  hint?: string
  type?: 'text' | 'date' | 'time' | 'number' | 'search'
  /** Render a money field with the shared two-decimal, thousands-separated format. */
  currency?: boolean
  placeholder?: string
  className?: string
}

/** A labelled text control of the workbench form. */
export function WorkbenchTextField({
  id,
  label,
  value,
  onChange,
  error,
  hint,
  type = 'text',
  currency = false,
  placeholder,
  className,
}: WorkbenchTextFieldProps) {
  return (
    <WorkbenchFieldShell id={id} label={label} error={error} hint={hint} className={className}>
      {currency ? (
        <CurrencyInput
          id={id}
          name={id}
          value={value}
          placeholder={placeholder}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          onValueChange={onChange}
          className={cn(CONTROL_CLASS, error ? INVALID_CONTROL_CLASS : undefined)}
        />
      ) : (
        <input
          id={id}
          name={id}
          type={type}
          value={value}
          placeholder={placeholder}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          onChange={(event) => onChange(event.target.value)}
          className={cn(CONTROL_CLASS, error ? INVALID_CONTROL_CLASS : undefined)}
        />
      )}
    </WorkbenchFieldShell>
  )
}

export interface WorkbenchTextAreaFieldProps {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  hint?: string
  placeholder?: string
  className?: string
}

/** A labelled multi-line control of the workbench form. */
export function WorkbenchTextAreaField({
  id,
  label,
  value,
  onChange,
  error,
  hint,
  placeholder,
  className,
}: WorkbenchTextAreaFieldProps) {
  return (
    <WorkbenchFieldShell id={id} label={label} error={error} hint={hint} className={className}>
      <textarea
        id={id}
        name={id}
        value={value}
        rows={3}
        placeholder={placeholder}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
        className={cn(
          CONTROL_CLASS,
          'h-auto py-2 leading-relaxed',
          error ? INVALID_CONTROL_CLASS : undefined,
        )}
      />
    </WorkbenchFieldShell>
  )
}

export interface WorkbenchSelectOption {
  value: string
  label: string
}

export interface WorkbenchSelectFieldProps {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  /** The options — every one of them comes from a server-published catalogue. */
  options: WorkbenchSelectOption[]
  /** The label of the empty option, e.g. "Select a client". */
  placeholder: string
  error?: string
  hint?: string
  className?: string
  disabled?: boolean
}

/** A labelled select control of the workbench form. */
export function WorkbenchSelectField({
  id,
  label,
  value,
  onChange,
  options,
  placeholder,
  error,
  hint,
  className,
  disabled,
}: WorkbenchSelectFieldProps) {
  return (
    <WorkbenchFieldShell id={id} label={label} error={error} hint={hint} className={className}>
      <select
        id={id}
        name={id}
        value={value}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
        className={cn(CONTROL_CLASS, error ? INVALID_CONTROL_CLASS : undefined)}
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </WorkbenchFieldShell>
  )
}

export interface WorkbenchCheckboxProps {
  id: string
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
  error?: string
  hint?: string
  className?: string
}

/** A labelled switch of the workbench form. */
export function WorkbenchCheckbox({
  id,
  label,
  checked,
  onChange,
  error,
  hint,
  className,
}: WorkbenchCheckboxProps) {
  return (
    <div className={cn('flex flex-col gap-1 min-w-0', className)}>
      <div className="flex items-center gap-2">
        <input
          id={id}
          name={id}
          type="checkbox"
          checked={checked}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          onChange={(event) => onChange(event.target.checked)}
          className="h-4 w-4 rounded border-artisan-border text-culinary-600 focus:ring-2 focus:ring-culinary-500/20"
        />
        <label htmlFor={id} className="font-body-sm text-body-sm text-espresso-700">
          {label}
        </label>
      </div>
      {error ? (
        <p id={`${id}-error`} role="alert" className="font-caption text-caption text-error">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="font-caption text-caption text-espresso-400">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
