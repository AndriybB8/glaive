import { useCallback, useEffect, useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Badge } from '../../components/ui/Badge'
import { ApiError } from '../../lib/http'
import { getCalendarItems } from '../calendar/api'
import { addDays, formatTime, todayIso } from '../calendar/dates'
import type { CalendarItem } from '../calendar/types'
import { getSchedules } from '../schedules/api'
import { changeTaskStatus } from '../tasks/api'
import { getUpcomingTasks } from './api'
import { buildDays, formatDay, plural, type DayPlan, type DayTask } from './todayTomorrow'

/** Zapas dni dla /api/tasks/upcoming: backend liczy „dziś” w UTC, a jutro lokalne musi się zmieścić. */
const UPCOMING_DAYS = 3
const UPCOMING_LIMIT = 50

interface WidgetData {
  days: DayPlan[]
  /** Część danych się nie wczytała – reszta jest pokazana, a tu komunikat. */
  partialError: string | null
}

/** Godziny wydarzenia w obrębie dnia; wydarzenie przez północ: „od 23:00” albo „do 01:00”. */
function eventTime(item: CalendarItem, day: string): string {
  if (item.isAllDay || !item.startsAt || !item.endsAt) return 'cały dzień'
  const startsToday = new Date(item.startsAt).toDateString() === dateOf(day).toDateString()
  const endsToday = new Date(new Date(item.endsAt).getTime() - 1).toDateString() === dateOf(day).toDateString()
  if (startsToday && endsToday) return `${formatTime(item.startsAt)}–${formatTime(item.endsAt)}`
  if (startsToday) return `od ${formatTime(item.startsAt)}`
  if (endsToday) return `do ${formatTime(item.endsAt)}`
  return 'cały dzień'
}

function dateOf(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

async function loadWidget(): Promise<WidgetData> {
  const today = todayIso()
  const [items, tasks, schedules] = await Promise.allSettled([
    getCalendarItems(today, addDays(today, 1)),
    getUpcomingTasks(UPCOMING_DAYS, UPCOMING_LIMIT),
    getSchedules(),
  ])
  if (items.status === 'rejected' && tasks.status === 'rejected') throw items.reason

  const problems = [items.status === 'rejected' && 'zajęć', tasks.status === 'rejected' && 'zadań'].filter(Boolean)
  return {
    days: buildDays(
      today,
      items.status === 'fulfilled' ? items.value : [],
      tasks.status === 'fulfilled' ? tasks.value : [],
      schedules.status === 'fulfilled' ? schedules.value : [],
    ),
    partialError: problems.length > 0 ? `Nie udało się wczytać ${problems.join(' ani ')}. Spróbuj odświeżyć.` : null,
  }
}

/**
 * Widżet „Dziś i jutro” (SCRUM-115): zajęcia z kalendarza i nieukończone zadania z terminem na dziś i jutro,
 * z własnych i udostępnionych harmonogramów. Zadanie można odhaczyć tu, jeśli rola w harmonogramie na to pozwala.
 * Dane odświeżają się po powrocie do karty przeglądarki (także po północy – wtedy „dziś” przesuwa się samo).
 */
export function TodayTomorrowWidget() {
  const [data, setData] = useState<WidgetData | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [completingId, setCompletingId] = useState<string | null>(null)
  const [announcement, setAnnouncement] = useState('')

  const reload = useCallback(async () => {
    try {
      const next = await loadWidget()
      setData(next)
      setLoadError(null)
    } catch {
      setLoadError('Nie udało się wczytać planu na dziś i jutro. Sprawdź, czy backend jest uruchomiony.')
    }
  }, [])

  useEffect(() => {
    let ignore = false
    loadWidget()
      .then((next) => {
        if (!ignore) setData(next)
      })
      .catch(() => {
        if (!ignore) setLoadError('Nie udało się wczytać planu na dziś i jutro. Sprawdź, czy backend jest uruchomiony.')
      })

    const handleVisible = () => {
      if (document.visibilityState === 'visible') void reload()
    }
    document.addEventListener('visibilitychange', handleVisible)
    return () => {
      ignore = true
      document.removeEventListener('visibilitychange', handleVisible)
    }
  }, [reload])

  const removeTask = (taskId: string) =>
    setData((current) =>
      current && { ...current, days: current.days.map((day) => ({ ...day, tasks: day.tasks.filter((t) => t.task.id !== taskId) })) },
    )

  const handleComplete = async ({ task }: DayTask) => {
    setActionError(null)
    setCompletingId(task.id)
    try {
      await changeTaskStatus(task.id, 'done')
      removeTask(task.id)
      setAnnouncement(`Oznaczono „${task.title}” jako zrobione.`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        removeTask(task.id)
        setActionError(`Zadania „${task.title}” już nie ma – mogło zostać usunięte.`)
      } else if (error instanceof ApiError && error.status === 403) {
        // Właściciel zmienił nam rolę na „Podgląd” – wczytujemy role od nowa, pole wyboru zniknie.
        setActionError(error.message)
        void reload()
      } else {
        setActionError(`Nie udało się oznaczyć „${task.title}” jako zrobione.`)
      }
    } finally {
      setCompletingId(null)
    }
  }

  return (
    <section aria-labelledby="today-tomorrow-title">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h2 id="today-tomorrow-title" className="text-lg font-semibold text-slate-900">
          Dziś i jutro
        </h2>
        <a href="#kalendarz" className="text-sm font-medium text-blue-700 hover:underline">
          Otwórz kalendarz
        </a>
      </div>

      <p role="status" className="sr-only">
        {announcement}
      </p>
      {actionError && (
        <div className="mb-3">
          <Alert tone="error">{actionError}</Alert>
        </div>
      )}

      {loadError && !data ? (
        <div className="rounded-xl border border-slate-200 p-4 text-sm text-slate-600">
          <p>{loadError}</p>
          <button type="button" onClick={() => void reload()} className="mt-2 font-medium text-blue-700 hover:underline">
            Spróbuj ponownie
          </button>
        </div>
      ) : !data ? (
        <p role="status" className="text-sm text-slate-500">
          Wczytywanie…
        </p>
      ) : (
        <>
          {data.partialError && (
            <div className="mb-3">
              <Alert tone="error">{data.partialError}</Alert>
            </div>
          )}
          <div className="grid gap-4 md:grid-cols-2">
            {data.days.map((day) => (
              <DayColumn key={day.date} day={day} completingId={completingId} onComplete={(t) => void handleComplete(t)} />
            ))}
          </div>
        </>
      )}
    </section>
  )
}

interface DayColumnProps {
  day: DayPlan
  completingId: string | null
  onComplete: (task: DayTask) => void
}

function DayColumn({ day, completingId, onComplete }: DayColumnProps) {
  const headingId = `day-${day.date}`
  const summary =
    day.events.length + day.tasks.length === 0
      ? 'Nic zaplanowanego'
      : [
          day.events.length > 0 && `${day.events.length} ${plural(day.events.length, 'wydarzenie', 'wydarzenia', 'wydarzeń')}`,
          day.tasks.length > 0 && `${day.tasks.length} ${plural(day.tasks.length, 'zadanie', 'zadania', 'zadań')}`,
        ]
          .filter(Boolean)
          .join(', ')

  return (
    <section aria-labelledby={headingId} className="flex flex-col rounded-xl border border-slate-200 bg-white p-4">
      <header className="flex items-baseline justify-between gap-3">
        <div className="min-w-0">
          <h3 id={headingId} className="text-base font-semibold text-slate-900">
            {day.label}
          </h3>
          <p className="text-sm text-slate-600 first-letter:uppercase">
            <time dateTime={day.date}>{formatDay(day.date)}</time>
          </p>
        </div>
        <p className="shrink-0 text-xs text-slate-500">{summary}</p>
      </header>

      <h4 className="mt-4 text-sm font-medium text-slate-700">Zajęcia i wydarzenia</h4>
      {day.events.length === 0 ? (
        <p className="mt-1 text-sm text-slate-500">Brak wydarzeń w kalendarzu.</p>
      ) : (
        <ul className="mt-1 divide-y divide-slate-100">
          {day.events.map((event) => (
            <li key={event.key} className="flex items-baseline gap-3 py-2 text-sm">
              <span className="w-24 shrink-0 text-slate-600 tabular-nums">{eventTime(event, day.date)}</span>
              <span aria-hidden="true" className="size-2 shrink-0 translate-y-[-1px] rounded-full" style={{ backgroundColor: event.color ?? '#2563EB' }} />
              <span className="min-w-0 flex-1 break-words text-slate-900">{event.title}</span>
            </li>
          ))}
        </ul>
      )}

      <h4 className="mt-4 text-sm font-medium text-slate-700">Zadania z terminem</h4>
      {day.tasks.length === 0 ? (
        <p className="mt-1 text-sm text-slate-500">Brak zadań na ten dzień.</p>
      ) : (
        <ul className="mt-1 divide-y divide-slate-100">
          {day.tasks.map((item) => (
            <TaskRow key={item.task.id} item={item} busy={completingId === item.task.id} onComplete={() => onComplete(item)} />
          ))}
        </ul>
      )}
    </section>
  )
}

interface TaskRowProps {
  item: DayTask
  busy: boolean
  onComplete: () => void
}

function TaskRow({ item, busy, onComplete }: TaskRowProps) {
  const { task, scheduleName, canComplete } = item
  const checkboxId = `complete-${task.id}`
  return (
    <li className="flex items-start gap-3 py-2 text-sm">
      {canComplete ? (
        <input
          id={checkboxId}
          type="checkbox"
          checked={busy}
          disabled={busy}
          onChange={onComplete}
          aria-label={`Oznacz „${task.title}” jako zrobione`}
          className="mt-0.5 size-5 shrink-0 cursor-pointer accent-blue-600 disabled:cursor-wait sm:size-4"
        />
      ) : (
        <span
          title="Tylko podgląd – zadanie zmienia właściciel albo osoba z prawem edycji"
          className="mt-0.5 size-5 shrink-0 rounded border border-dashed border-slate-300 sm:size-4"
        />
      )}
      <div className="min-w-0 flex-1">
        <p className="break-words text-slate-900">{task.title}</p>
        <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1">
          {scheduleName && <span className="text-xs text-slate-500">{scheduleName}</span>}
          {task.status === 'inProgress' && <Badge tone="blue">W trakcie</Badge>}
          {task.priority === 'high' && <Badge tone="neutral">Wysoki priorytet</Badge>}
          {!canComplete && <span className="sr-only">Tylko podgląd.</span>}
        </div>
      </div>
    </li>
  )
}
