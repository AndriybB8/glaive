import type { ReactNode } from 'react'

const toneClasses = {
  blue: 'border-transparent bg-blue-50 text-blue-700',
  neutral: 'border-transparent bg-slate-100 text-slate-600',
  muted: 'border-slate-200 bg-white text-slate-500',
}

interface BadgeProps {
  tone: keyof typeof toneClasses
  children: ReactNode
}

/** Status w postaci słowa (np. „Trwa”). Znaczenie niesie tekst, kolor tylko je wzmacnia. */
export function Badge({ tone, children }: BadgeProps) {
  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap ${toneClasses[tone]}`}>
      {children}
    </span>
  )
}
