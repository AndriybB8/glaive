import type { LiveStatus } from './realtime'

const LABELS: Record<LiveStatus, { text: string; dot: string; title: string }> = {
  connected: { text: 'Na żywo', dot: 'bg-emerald-500', title: 'Zmiany innych osób pojawiają się od razu.' },
  connecting: { text: 'Łączenie…', dot: 'bg-slate-400', title: 'Łączenie z serwerem, żeby zmiany innych osób pojawiały się od razu.' },
  reconnecting: { text: 'Łączenie…', dot: 'bg-amber-500', title: 'Połączenie zerwane – trwa ponowne łączenie.' },
  offline: {
    text: 'Bez aktualizacji na żywo',
    dot: 'bg-slate-400',
    title: 'Nie udało się połączyć. Zmiany innych osób zobaczysz po odświeżeniu; aplikacja próbuje połączyć się ponownie.',
  },
}

/** Mały wskaźnik stanu połączenia na żywo pod nagłówkiem „Listy”. */
export function LiveStatusBadge({ status }: { status: LiveStatus }) {
  const label = LABELS[status]
  return (
    <p className="flex items-center gap-1.5 text-xs text-slate-500" title={label.title}>
      <span aria-hidden className={`size-2 rounded-full ${label.dot} ${status === 'connected' ? '' : 'animate-pulse'}`} />
      {label.text}
    </p>
  )
}
