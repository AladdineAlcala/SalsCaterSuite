import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { Combobox } from './Combobox'

describe('Combobox', () => {
  const options = [
    { value: '1', label: 'New York' },
    { value: '2', label: 'Los Angeles' },
    { value: '3', label: 'Chicago' },
    { value: '4', label: 'Houston' },
  ]

  it('renders with placeholder when no value is selected', () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    expect(screen.getByText('Select a city')).toBeInTheDocument()
  })

  it('displays the selected option when a value is selected', () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value="1"
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    expect(screen.getByText('New York')).toBeInTheDocument()
  })

  it('opens the dropdown when clicked', async () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const combobox = screen.getByRole('button')
    await userEvent.click(combobox)

    expect(screen.getByRole('listbox')).toBeInTheDocument()
  })

  it('filters options based on search input', async () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const combobox = screen.getByRole('button')
    await userEvent.click(combobox)

    const input = screen.getByPlaceholderText('Select a city')
    await userEvent.type(input, 'new')

    expect(screen.getByText('New York')).toBeInTheDocument()
    expect(screen.queryByText('Los Angeles')).not.toBeInTheDocument()
    expect(screen.queryByText('Chicago')).not.toBeInTheDocument()
    expect(screen.queryByText('Houston')).not.toBeInTheDocument()
  })

  it('selects an option when clicked', async () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const combobox = screen.getByRole('button')
    await userEvent.click(combobox)

    const losAngelesOption = screen.getByRole('option', { name: /Los Angeles/i })
    await userEvent.click(losAngelesOption)

    expect(handleChange).toHaveBeenCalledWith('2')
  })

  it('closes dropdown after selection', async () => {
    const handleChange = vi.fn()
    const { rerender } = render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const combobox = screen.getByRole('button')
    await userEvent.click(combobox)

    const losAngelesOption = screen.getByRole('option', { name: /Los Angeles/i })
    await userEvent.click(losAngelesOption)

    rerender(
      <Combobox
        id="test-combobox"
        value="2"
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('clears selection when clear button is clicked', async () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value="1"
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const clearButton = screen.getByLabelText('Clear selection')
    await userEvent.click(clearButton)

    expect(handleChange).toHaveBeenCalledWith('')
  })

  it('displays "No options match" when search yields no results', async () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
      />,
    )

    const combobox = screen.getByRole('button')
    await userEvent.click(combobox)

    const input = screen.getByPlaceholderText('Select a city')
    await userEvent.type(input, 'xyz')

    expect(screen.getByText("No options match your search")).toBeInTheDocument()
  })

  it('disables the combobox when disabled prop is true', () => {
    const handleChange = vi.fn()
    render(
      <Combobox
        id="test-combobox"
        value=""
        onChange={handleChange}
        options={options}
        placeholder="Select a city"
        disabled={true}
      />,
    )

    expect(screen.getByRole('button')).toHaveClass('cursor-not-allowed')
  })
})
