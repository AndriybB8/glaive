import { useEffect, useRef, useState } from 'react'
import { readableTextColor } from '../tags/tagColors'
import { formatTime, fromIsoDate, todayIso } from './dates'
import { describeItem } from './describeItem'
import { ItemChip } from './ItemChip'
import { allDayItemsOn, itemColor, timedSegmentsOn } from './layout'
import type { CalendarItem } from './types'

/** Wysokość godziny w px – 30 min to 24 px, wygodne do kliknięcia. */
const HOUR_HEIGHT = 48
/** Po otwarciu widok przewija się do tej godziny (noc zwykle jest pusta). */
const SCROLL_TO_HOUR = 7
const HOURS = Array.from({ length: 24 }, (_, hour) => hour)

const headerFormat = new Intl.DateTimeFormat('pl-PL', { weekday: 'short' })
const fullDayFormat = new Intl.DateTimeFormat('pl-PL', { weekday: 'long', day: 'numeric', month: 'long' })

interface TimeGridViewProps {
  /** Pokazywane dni: 7 (tydzień) albo 1 (dzień). */
  days: string[]
  items: readonly CalendarItem[]
  onOpenItem: (item: CalendarItem) => void
  onShowDay: (day: string) => void
  /** Kliknięcie w pustą godzinę – nowe wydarzenie o tej porze (minuty od północy, co 30 min); bez godziny – całodniowe. */
  onCreate: (day: string, minutes?: number) => void
}

function useMinutesNow(): number {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])
  return now.getHours() * 60 + now.getMinutes()
}

export function TimeGridView({ days, items, onOpenItem, onShowDay, onCreate }: TimeGridViewProps) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const today = todayIso()
  const minutesNow = useMinutesNow()
  const single = days.length === 1

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: SCROLL_TO_HOUR * HOUR_HEIGHT })
  }, [days.length])

  const columns = { gridTemplateColumns: `3.5rem repeat(${days.length}, minmax(0, 1fr))` }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Nagłówki dni i pasek elementów całodniowych (harmonogramy, zadania, wydarzenia na cały dzień). */}
      <div className="grid border-b border-slate-200" style={columns}>
        <div className="flex items-end justify-end pr-1.5 pb-1.5 text-right text-[10px] leading-tight text-slate-400">cały dzień</div>
        {days.map((day) => {
          const date = fromIsoDate(day)
          const isToday = day === today
          return (
            <div key={day} onClick={() => onCreate(day)} className="flex min-w-0 cursor-pointer flex-col gap-0.5 border-l border-slate-200 p-1 hover:bg-slate-50">
              <button
                type="button"
                onClick={(event) => {
                  event.stopPropagation()
                  onShowDay(day)
                }}
                disabled={single}
                aria-label={`${fullDayFormat.format(date)} – pokaż dzień`}
                aria-current={isToday ? 'date' : undefined}
                className="mb-0.5 flex flex-col items-center rounded-md py-0.5 hover:bg-slate-100 disabled:cursor-default disabled:hover:bg-transparent"
              >
                <span className={`text-[11px] uppercase ${isToday ? 'font-semibold text-blue-700' : 'text-slate-500'}`}>{headerFormat.format(date)}</span>
                <span
                  className={`flex size-7 items-center justify-center rounded-full text-sm tabular-nums ${
                    isToday ? 'bg-blue-600 font-semibold text-white' : 'text-slate-800'
                  }`}
                >
                  {date.getDate()}
                </span>
              </button>
              {allDayItemsOn(items, day).map((item) => (
                <ItemChip key={item.key} item={item} onOpen={onOpenItem} />
              ))}
            </div>
          )
        })}
      </div>

      {/* Siatka godzin. */}
      <div ref={scrollRef} className="min-h-0 flex-1 overflow-y-auto">
        <div className="grid" style={{ ...columns, height: 24 * HOUR_HEIGHT }}>
          <div className="relative" aria-hidden>
            {HOURS.slice(1).map((hour) => (
              <span key={hour} className="absolute right-2 -translate-y-1/2 text-[11px] text-slate-400 tabular-nums" style={{ top: hour * HOUR_HEIGHT }}>
                {String(hour).padStart(2, '0')}:00
              </span>
            ))}
          </div>

          {days.map((day) => (
            <div
              key={day}
              className="relative cursor-pointer border-l border-slate-200"
              onClick={(event) => {
                // Pół godziny, w które kliknięto.
                const y = event.clientY - event.currentTarget.getBoundingClientRect().top
                onCreate(day, Math.min(23 * 60 + 30, Math.floor(y / (HOUR_HEIGHT / 2)) * 30))
              }}
            >
              {HOURS.map((hour) => (
                <div key={hour} aria-hidden className="pointer-events-none absolute inset-x-0 border-t border-slate-100" style={{ top: hour * HOUR_HEIGHT }} />
              ))}

              {timedSegmentsOn(items, day).map((segment) => {
                const { item } = segment
                const color = itemColor(item)
                const height = ((segment.bottom - segment.top) / 60) * HOUR_HEIGHT
                const label = describeItem(item)
                return (
                  <button
                    key={item.key}
                    type="button"
                    title={label}
                    aria-label={label}
                    onClick={(event) => {
                      event.stopPropagation()
                      onOpenItem(item)
                    }}
                    className={`absolute overflow-hidden border border-white px-1.5 text-left text-xs leading-4 hover:brightness-95 focus-visible:z-10 focus-visible:outline-2 focus-visible:outline-blue-600 ${
                      segment.continuesBefore ? 'rounded-t-none' : 'rounded-t-md'
                    } ${segment.continuesAfter ? 'rounded-b-none' : 'rounded-b-md'}`}
                    style={{
                      top: (segment.top / 60) * HOUR_HEIGHT,
                      height,
                      left: `calc(${(segment.column / segment.columns) * 100}% + 1px)`,
                      width: `calc(${100 / segment.columns}% - 3px)`,
                      backgroundColor: color,
                      color: readableTextColor(color),
                    }}
                  >
                    <span className="block truncate font-medium">{item.title}</span>
                    {height >= 32 && (
                      <span className="block truncate opacity-90 tabular-nums">
                        {formatTime(item.startsAt!)}–{formatTime(item.endsAt!)}
                      </span>
                    )}
                  </button>
                )
              })}

              {day === today && (
                <div aria-hidden className="pointer-events-none absolute inset-x-0 z-10 flex items-center" style={{ top: (minutesNow / 60) * HOUR_HEIGHT }}>
                  <span className="-ml-1 size-2 rounded-full bg-red-500" />
                  <span className="h-0.5 flex-1 bg-red-500" />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
