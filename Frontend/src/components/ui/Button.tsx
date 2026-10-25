import type { ButtonHTMLAttributes } from 'react'

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'ghost-danger' | 'danger' | 'warning' | 'warning-outline'

// Wypełnione warianty mają dokładnie klasy przycisku „Nowa notatka”. Obramowanie wariantów konturowych
// to ring (inset), żeby wszystkie przyciski miały tę samą wysokość.
const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-blue-600 px-3 py-1.5 font-medium text-white hover:bg-blue-700 focus-visible:outline-offset-2',
  secondary: 'bg-white px-3 py-1.5 font-medium text-slate-700 ring-1 ring-slate-300 ring-inset hover:bg-slate-50 focus-visible:outline-offset-2',
  ghost: 'px-2 py-1 text-slate-600 hover:bg-slate-100',
  'ghost-danger': 'px-2 py-1 text-slate-500 hover:bg-red-50 hover:text-red-700',
  danger: 'bg-red-600 px-3 py-1.5 font-medium text-white hover:bg-red-700 focus-visible:outline-offset-2',
  warning: 'bg-amber-900 px-3 py-1.5 font-medium text-white hover:bg-amber-800',
  'warning-outline': 'px-3 py-1.5 font-medium ring-1 ring-amber-400 ring-inset hover:bg-amber-100',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
}

/** Przycisk tekstowy. Warianty jak w design systemie (Button); `primary` – jedna główna akcja widoku. */
export function Button({ variant = 'primary', type = 'button', className = '', ...props }: ButtonProps) {
  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center rounded-md text-sm focus-visible:outline-2 focus-visible:outline-blue-600 disabled:cursor-not-allowed disabled:opacity-60 ${variantClasses[variant]} ${className}`}
      {...props}
    />
  )
}
