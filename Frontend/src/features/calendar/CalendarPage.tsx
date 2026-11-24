import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { LiveStatusBadge } from '../../components/ui/LiveStatusBadge'
import { useScheduleHub } from '../schedules/realtime'
import { getCalendarItems } from './api'
import { daysBetween, rangeTitle, shiftAnchor, todayIso, visibleRange } from './dates'
import { DeleteEventDialog } from './DeleteEventDialog'
import { EventFormDialog, type NewEventSlot } from './EventFormDialog'
import { ItemDetailsDialog } from './ItemDetailsDialog'
import { MonthView } from './MonthView'
import { TimeGridView } from './TimeGridView'
import type { CalendarItem, CalendarView } from './types'

const VIEWS: { value: CalendarView; label: string }[] = [
  { value: 'day', label: 'Dzień' },
  { value: 'week', label: 'Tydzień' },
  { value: 'month', label: 'Miesiąc' },
]
const VIEW_STORAGE_KEY = 'SP.calendarView'
/** Zmiany na żywo przychodzą seriami – kalendarz pobieramy raz po chwili ciszy. */
const LIVE_REFRESH_DELAY_MS = 300

type OpenDialog =
  | { kind: 'create'; slot: NewEventSlot }
  | { kind: 'edit'; item: CalendarItem }
  | { kind: 'delete'; item: CalendarItem }
  | { kind: 'details'; item: CalendarItem }
  | null

/** Zapamiętany widok; na wąskim ekranie domyślnie dzień (tydzień i miesiąc są tam ciasne). */
function initialView(): CalendarView {
  try {
    const saved = localStorage.getItem(VIEW_STORAGE_KEY)
    if (saved === 'day' || saved === 'week' || saved === 'month') return saved
  } catch {
    // Zablokowany localStorage – zostaje widok domyślny.
  }
  return window.matchMedia('(min-width: 640px)').matches ? 'week' : 'day'
}

function ChevronIcon({ direction }: { direction: 'left' | 'right' }) {
  return (
    <svg viewBox="0 0 20 20" className="size-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={direction === 'left' ? 'm12 5-5 5 5 5' : 'm8 5 5 5-5 5'} />
    </svg>
  )
}

/**
 * Kalendarz: własne wydarzenia (jednorazowe i serie), harmonogramy i zadania z terminem – także udostępnione przez innych.
 * Zmiany innych osób w harmonogramach przychodzą przez hub harmonogramów (SCRUM-109) i odświeżają widok na żywo.
 */
export function CalendarPage() {
  const [view, setView] = useState<CalendarView>(initialView)
  const [anchor, setAnchor] = useState(todayIso)
  const [items, setItems] = useState<CalendarItem[] | null>(null)
  /** Zakres („from|to”), którego dotyczą `items` – inny niż widoczny = trwa wczytywanie. */
  const [loadedRange, setLoadedRange] = useState<string | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)
  const requestRef = useRef(0)
  const liveTimerRef = useRef<number | undefined>(undefined)

  const { from, to } = visibleRange(view, anchor)

  /** Pobiera widoczny zakres. Odpowiedź na starszy zakres (szybkie przełączanie) jest pomijana. */
  const load = useCallback(async (rangeFrom: string, rangeTo: string, quiet = false) => {
    const requestId = ++requestRef.current
    try {
      const result = await getCalendarItems(rangeFrom, rangeTo)
      if (requestId !== requestRef.current) return
      setItems(result)
      setLoadedRange(`${rangeFrom}|${rangeTo}`)
      setLoadError(null)
    } catch {
      if (requestId !== requestRef.current) return
      // Odświeżanie w tle nie zasłania kalendarza błędem – zostaje poprzedni stan.
      if (!quiet) {
        setLoadError('Nie udało się wczytać kalendarza. Sprawdź, czy backend jest uruchomiony.')
        setLoadedRange(`${rangeFrom}|${rangeTo}`)
      }
    }
  }, [])

  const loading = loadedRange !== `${from}|${to}`

  // Ten sam przebieg co load(), ale w .then – stan ustawia się dopiero po odpowiedzi.
  useEffect(() => {
    const requestId = ++requestRef.current
    getCalendarItems(from, to)
      .then((result) => {
        if (requestId !== requestRef.current) return
        setItems(result)
        setLoadedRange(`${from}|${to}`)
        setLoadError(null)
      })
      .catch(() => {
        if (requestId !== requestRef.current) return
        setLoadError('Nie udało się wczytać kalendarza. Sprawdź, czy backend jest uruchomiony.')
        setLoadedRange(`${from}|${to}`)
      })
  }, [from, to])

  /** Aktualny zakres dla odświeżania z timera i zdarzeń okna. */
  const rangeRef = useRef({ from, to })
  useEffect(() => {
    rangeRef.current = { from, to }
  })

  const refreshQuietly = useCallback(() => {
    window.clearTimeout(liveTimerRef.current)
    liveTimerRef.current = window.setTimeout(() => void load(rangeRef.current.from, rangeRef.current.to, true), LIVE_REFRESH_DELAY_MS)
  }, [load])

  useEffect(() => () => window.clearTimeout(liveTimerRef.current), [])

  // Własne wydarzenia nie mają kanału na żywo – po powrocie do karty pobieramy kalendarz, żeby zobaczyć zmiany z innej karty.
  useEffect(() => {
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') refreshQuietly()
    }
    document.addEventListener('visibilitychange', handleVisibility)
    return () => document.removeEventListener('visibilitychange', handleVisibility)
  }, [refreshQuietly])

  // Harmonogramy i zadania (także cudze, udostępnione) – każda zmiana na żywo odświeża widok.
  const liveStatus = useScheduleHub({
    onScheduleChanged: refreshQuietly,
    onInvitationReceived: () => undefined,
    onResync: refreshQuietly,
  })

  const changeView = (next: CalendarView) => {
    setView(next)
    try {
      localStorage.setItem(VIEW_STORAGE_KEY, next)
    } catch {
      // Bez zapamiętania – nic się nie dzieje.
    }
  }

  const showDay = (day: string) => {
    setAnchor(day)
    changeView('day')
  }

  const openItem = (item: CalendarItem) => setDialog(item.kind === 'event' ? { kind: 'edit', item } : { kind: 'details', item })

  const afterChange = () => {
    setDialog(null)
    void load(from, to, true)
  }

  const isCurrent = from <= todayIso() && todayIso() <= to

  return (
    <div className="flex h-full flex-col bg-white text-slate-900">
      <header className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-slate-200 px-4 py-3 sm:px-6">
        <div className="flex items-center gap-1">
          <Button variant="secondary" onClick={() => setAnchor(todayIso())} disabled={isCurrent} className="mr-1">
            Dziś
          </Button>
          <IconButton label="Poprzedni okres" title="Poprzedni" touch onClick={() => setAnchor(shiftAnchor(view, anchor, -1))}>
            <ChevronIcon direction="left" />
          </IconButton>
          <IconButton label="Następny okres" title="Następny" touch onClick={() => setAnchor(shiftAnchor(view, anchor, 1))}>
            <ChevronIcon direction="right" />
          </IconButton>
        </div>

        <div className="flex min-w-0 flex-1 basis-48 flex-col">
          <h1 className="text-base font-semibold sm:text-lg" aria-live="polite">
            {rangeTitle(view, anchor)}
          </h1>
          <LiveStatusBadge status={liveStatus} />
        </div>

        <div role="radiogroup" aria-label="Widok kalendarza" className="flex rounded-md bg-slate-100 p-0.5">
          {VIEWS.map((option) => (
            <button
              key={option.value}
              type="button"
              role="radio"
              aria-checked={view === option.value}
              onClick={() => changeView(option.value)}
              className={`min-h-9 rounded px-3 text-sm sm:min-h-0 sm:py-1 ${
                view === option.value ? 'bg-white font-medium text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>

        <Button onClick={() => setDialog({ kind: 'create', slot: { day: view === 'day' ? anchor : isCurrent ? todayIso() : from, minutes: 9 * 60 } })}>
          Nowe wydarzenie
        </Button>
      </header>

      {loadError && (
        <div className="px-4 pt-3 sm:px-6">
          <Alert tone="error">
            {loadError}{' '}
            <button type="button" onClick={() => void load(from, to)} className="font-medium underline">
              Spróbuj ponownie
            </button>
          </Alert>
        </div>
      )}

      <div aria-busy={loading} className={`flex min-h-0 flex-1 flex-col transition-opacity ${loading && items !== null ? 'opacity-60' : ''}`}>
        {view === 'month' ? (
          <MonthView
            from={from}
            to={to}
            anchor={anchor}
            items={items ?? []}
            onOpenItem={openItem}
            onShowDay={showDay}
            onCreate={(day) => setDialog({ kind: 'create', slot: { day } })}
          />
        ) : (
          <TimeGridView
            days={daysBetween(from, to)}
            items={items ?? []}
            onOpenItem={openItem}
            onShowDay={showDay}
            onCreate={(day, minutes) => setDialog({ kind: 'create', slot: { day, minutes } })}
          />
        )}
      </div>

      {dialog?.kind === 'create' && (
        <EventFormDialog slot={dialog.slot} onClose={() => setDialog(null)} onSaved={afterChange} onDelete={() => undefined} onChangedElsewhere={refreshQuietly} />
      )}
      {dialog?.kind === 'edit' && (
        <EventFormDialog
          key={dialog.item.key}
          item={dialog.item}
          onClose={() => setDialog(null)}
          onSaved={afterChange}
          onDelete={(item) => setDialog({ kind: 'delete', item })}
          onChangedElsewhere={refreshQuietly}
        />
      )}
      {dialog?.kind === 'delete' && <DeleteEventDialog item={dialog.item} onClose={() => setDialog(null)} onDeleted={afterChange} />}
      {dialog?.kind === 'details' && <ItemDetailsDialog item={dialog.item} onClose={() => setDialog(null)} />}
    </div>
  )
}
