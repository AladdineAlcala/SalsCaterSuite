import type { ButtonHTMLAttributes, ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/utils/cn'

export type ButtonVariant = 'primary' | 'secondary' | 'tertiary'
export type ButtonSize = 'sm' | 'md'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: LucideIcon
  children?: ReactNode
  /**
   * Renders the button as unavailable **while keeping it focusable**, so the
   * `aria-disabled` state and the explanatory tooltip stay reachable by
   * keyboard. Use it for actions whose target module is not implemented.
   */
  inactive?: boolean
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary:
    'bg-culinary-600 text-white hover:bg-culinary-700 active:scale-[0.99] hover:scale-[1.01] shadow-elevation-3',
  secondary:
    'bg-artisan-surface text-espresso-850 border border-artisan-border hover:border-artisan-border-contrast hover:bg-surface-container-low shadow-elevation-1',
  tertiary: 'bg-transparent text-espresso-600 hover:bg-artisan-subtle hover:text-espresso-900',
}

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'h-9 px-3 text-body-sm',
  md: 'h-10 px-space-lg text-body-md',
}

/**
 * Design-system button (DESIGN.md "Buttons").
 *
 * Primary  — solid culinary emerald with the amber-free elevation glow.
 * Secondary — white surface with the linen hairline border.
 * Tertiary — transparent, for utility actions.
 */
export function Button({
  variant = 'secondary',
  size = 'md',
  icon: Icon,
  className,
  type = 'button',
  inactive = false,
  onClick,
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      aria-disabled={inactive || undefined}
      onClick={
        inactive
          ? (event) => {
              event.preventDefault()
            }
          : onClick
      }
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg font-headline-sm transition-all',
        'disabled:cursor-not-allowed disabled:opacity-60 disabled:shadow-none',
        inactive && 'cursor-not-allowed opacity-60 shadow-none',
        SIZE_CLASSES[size],
        VARIANT_CLASSES[variant],
        className,
      )}
      {...rest}
    >
      {Icon ? <Icon className="h-4 w-4" aria-hidden="true" /> : null}
      {children}
    </button>
  )
}
