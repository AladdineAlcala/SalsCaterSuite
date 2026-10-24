import { useState, type InputHTMLAttributes } from 'react'
import { formatCurrencyInput, normalizeCurrencyInput } from '@/utils/currencyInput'

export interface CurrencyInputProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'value' | 'onChange'> {
  /** The raw field value the form holds — never a formatted string. */
  value: string
  /** Receives the raw, unformatted value on every keystroke. */
  onValueChange: (value: string) => void
}

/**
 * A currency text control (owner request 2026-10-24).
 *
 * It shows `1,425.00` when the operator leaves the field and the raw value while
 * it is being edited. The form state stays **raw**, so a comma never reaches a
 * parser. The peso symbol is intentionally absent: the field is editable text,
 * not a readout (see `utils/currencyInput.ts`).
 */
export function CurrencyInput({
  value,
  onValueChange,
  onFocus,
  onBlur,
  ...props
}: CurrencyInputProps) {
  const [isFocused, setIsFocused] = useState(false)
  const [focusedValue, setFocusedValue] = useState('')

  // The display is derived on every render: a value seeded after the first paint
  // (an edit form's detail query) is formatted immediately, without an effect.
  const display = isFocused ? focusedValue : formatCurrencyInput(value)

  return (
    <input
      {...props}
      type="text"
      inputMode="decimal"
      autoComplete="off"
      value={display}
      onFocus={(event) => {
        setFocusedValue(value)
        setIsFocused(true)
        onFocus?.(event)
      }}
      onChange={(event) => {
        const raw = normalizeCurrencyInput(event.target.value)
        setFocusedValue(raw)
        onValueChange(raw)
      }}
      onBlur={(event) => {
        setIsFocused(false)
        setFocusedValue('')
        onBlur?.(event)
      }}
    />
  )
}
