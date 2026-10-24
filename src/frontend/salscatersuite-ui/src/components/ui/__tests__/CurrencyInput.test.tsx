import { useState } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CurrencyInput } from '@/components/ui/CurrencyInput'

function ControlledCurrencyInput({ initial }: { initial: string }) {
  const [value, setValue] = useState(initial)
  return (
    <label>
      Amount
      <CurrencyInput value={value} onValueChange={setValue} />
    </label>
  )
}

describe('CurrencyInput', () => {
  it('formats the value with two decimals and thousands separators when it is not focused', () => {
    render(<ControlledCurrencyInput initial="1450" />)

    expect(screen.getByLabelText('Amount')).toHaveValue('1,450.00')
  })

  it('shows the raw value while editing and keeps the form state raw', () => {
    render(<ControlledCurrencyInput initial="1450" />)
    const input = screen.getByLabelText('Amount')

    fireEvent.focus(input)
    expect(input).toHaveValue('1450')

    fireEvent.change(input, { target: { value: '2,000.50' } })
    expect(input).toHaveValue('2000.50')
  })

  it('formats again when the field loses focus', () => {
    render(<ControlledCurrencyInput initial="1450" />)
    const input = screen.getByLabelText('Amount')

    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: '2000.5' } })
    fireEvent.blur(input)

    expect(input).toHaveValue('2,000.50')
  })

  it('preserves a leading minus so the shared validator can reject it', () => {
    render(<ControlledCurrencyInput initial="" />)
    const input = screen.getByLabelText('Amount')

    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: '-1' } })

    expect(input).toHaveValue('-1')
  })
})
