import { useState, useRef, useEffect } from 'react'
import { ChevronDown, X } from 'lucide-react'
import { cn } from '@/utils/cn'

export interface ComboboxOption {
  value: string
  label: string
}

export interface ComboboxProps {
  id: string
  value: string
  onChange: (value: string) => void
  options: ComboboxOption[]
  placeholder?: string
  disabled?: boolean
  required?: boolean
  className?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

/**
 * A searchable combobox component with a dropdown list.
 * Allows users to type to filter the options.
 */
export function Combobox({
  id,
  value,
  onChange,
  options,
  placeholder = 'Select an option',
  disabled = false,
  required = false,
  className,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}: ComboboxProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [searchTerm, setSearchTerm] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const selectedOption = options.find((opt) => opt.value === value)

  const filteredOptions = options.filter((opt) =>
    opt.label.toLowerCase().includes(searchTerm.toLowerCase()),
  )

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus()
    }
  }, [isOpen])

  const handleSelect = (optionValue: string) => {
    onChange(optionValue)
    setIsOpen(false)
    setSearchTerm('')
  }

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    onChange('')
    setSearchTerm('')
  }

  return (
    <div ref={containerRef} className={cn('relative w-full', className)}>
      <div
        className={cn(
          'relative w-full h-10 rounded-lg bg-surface-container-lowest border border-artisan-border',
          'flex items-center transition-all duration-200',
          isOpen ? 'border-culinary-600 ring-2 ring-culinary-500/15' : 'hover:border-culinary-400',
          disabled && 'bg-surface-container opacity-50',
          ariaInvalid && 'border-error focus:border-error focus:ring-error/15',
        )}
      >
        <button
          id={id}
          type="button"
          value={value}
          disabled={disabled}
          aria-expanded={isOpen}
          aria-haspopup="listbox"
          aria-controls={`${id}-listbox`}
          aria-required={required}
          aria-invalid={ariaInvalid}
          aria-describedby={ariaDescribedBy}
          onClick={() => setIsOpen((open) => !open)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') setIsOpen(false)
          }}
          className={cn(
            'h-full flex-1 min-w-0 px-3 rounded-lg text-left',
            'text-body-sm font-body-sm truncate focus:outline-none focus-visible:ring-2 focus-visible:ring-culinary-500/20',
            disabled ? 'cursor-not-allowed' : 'cursor-pointer',
            selectedOption ? 'text-espresso-900' : 'text-espresso-400',
          )}
        >
          {selectedOption?.label || placeholder}
        </button>

        <div className="flex items-center gap-1.5 flex-shrink-0 pr-2">
          {value && !disabled && (
            <button
              type="button"
              onClick={handleClear}
              className="p-0.5 text-espresso-400 hover:text-espresso-700 transition-colors"
              aria-label="Clear selection"
            >
              <X className="h-4 w-4" />
            </button>
          )}
          <ChevronDown
            className={cn(
              'h-4 w-4 text-espresso-400 transition-transform duration-200 flex-shrink-0',
              isOpen && 'rotate-180',
            )}
            aria-hidden="true"
          />
        </div>
      </div>

      {isOpen && (
        <div
          className={cn(
            'absolute top-full left-0 right-0 mt-1 bg-white border border-artisan-border rounded-lg',
            'shadow-md z-50 overflow-hidden',
          )}
        >
          <input
            ref={inputRef}
            type="search"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder={placeholder}
            aria-label="Search options"
            aria-invalid={ariaInvalid}
            aria-describedby={ariaDescribedBy}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setIsOpen(false)
                containerRef.current?.querySelector('button')?.focus()
              }
            }}
            className={cn(
              'w-full h-10 px-3 text-espresso-900 text-body-sm font-body-sm',
              'border-b border-artisan-border outline-none focus:ring-2 focus:ring-inset focus:ring-culinary-500/15',
            )}
          />
          <div id={`${id}-listbox`} role="listbox" className="max-h-56 overflow-y-auto">
            {filteredOptions.length === 0 ? (
              <div className="px-3 py-2 text-body-sm text-espresso-400 text-center">
                No options match your search
              </div>
            ) : (
              filteredOptions.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => handleSelect(option.value)}
                  role="option"
                  aria-selected={value === option.value}
                  className={cn(
                    'w-full text-left px-3 py-2 text-body-sm font-body-sm transition-colors',
                    value === option.value
                      ? 'bg-culinary-100 text-culinary-900 font-semibold'
                      : 'text-espresso-900 hover:bg-surface-container-lowest',
                  )}
                >
                  {option.label}
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}
