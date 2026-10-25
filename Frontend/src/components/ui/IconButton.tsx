import type { ButtonHTMLAttributes } from 'react'

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** Polska nazwa akcji – trafia do aria-label i dymka. */
  label: string
  tone?: 'default' | 'danger'
  /** Na telefonie 44×44 px (łatwiej trafić palcem), od 640 px w górę 32×32 px. */
  touch?: boolean
}

/** Kwadratowy przycisk z ikoną (jak przyciski paska narzędzi w edytorze notatek). */
export function IconButton({ label, tone = 'default', touch = false, type = 'button', title, className = '', ...props }: IconButtonProps) {
  return (
    <button
      type={type}
      aria-label={label}
      title={title ?? label}
      className={`flex shrink-0 items-center justify-center rounded-md text-slate-600 transition-colors focus-visible:outline-2 focus-visible:outline-blue-600 disabled:cursor-not-allowed disabled:opacity-35 ${
        touch ? 'size-11 sm:size-8' : 'size-8'
      } ${tone === 'danger' ? 'hover:bg-red-50 hover:text-red-700' : 'hover:bg-slate-100 hover:text-slate-900'} ${className}`}
      {...props}
    />
  )
}
