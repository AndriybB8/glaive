import { daysBetween, fromIsoDate, todayIso } from './dates'
import { ItemChip } from './ItemChip'
import { itemColor, itemsOn } from './layout'
import type { CalendarItem } from './types'

const WEEKDAYS = ['Pn', 'Wt', 'Śr', 'Cz', 'Pt', 'So', 'Nd']
/** Tyle elementów mieści się w dniu; reszta pod „+N więcej” (otwiera widok dnia). */
const MAX_VISIBLE = 3
/** Na telefonie zamiast tekstu kolorowe kropki – tyle naraz. */
const MAX_DOTS = 4

/** Na wąskim ekranie dzień jest za wąski na tekst, więc dotknięcie otwiera widok dnia zamiast nowego wydarzenia. */
const isNarrow = () => !window.matchMedia('(min-width: 640px)').matches

const dayLabel = new Intl.DateTimeFormat('pl-PL', { weekday: 'long', day: 'numeric', month: 'long' })

interface MonthViewProps {
  from: string
  to: string
  /** Dowolny dzień pokazywanego miesiąca – dni spoza niego są wyszarzone. */
  anchor: string
  items: readonly CalendarItem[]
  onOpenItem: (item: CalendarItem) => void
  onShowDay: (day: string) => void
  /** Kliknięcie w puste miejsce dnia – nowe wydarzenie tego dnia. */
  onCreate: (day: string) => void
}

export function MonthView({ from, to, anchor, items, onOpenItem, onShowDay, onCreate }: MonthViewProps) {
  const days = daysBetween(from, to)
  const month = fromIsoDate(anchor).getMonth()
  const today = todayIso()

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="grid grid-cols-7 border-b border-slate-200 text-center text-xs font-medium text-slate-500" aria-hidden>
        {WEEKDAYS.map((name) => (
          <div key={name} className="py-2">
            {name}
          </div>
        ))}
      </div>
      <div className="grid min-h-0 flex-1 auto-rows-fr grid-cols-7">
        {days.map((day) => {
          const dayItems = itemsOn(items, day)
          const hidden = dayItems.length - MAX_VISIBLE
          const date = fromIsoDate(day)
          const inMonth = date.getMonth() === month
          const isToday = day === today
          return (
            <div
              key={day}
              onClick={() => (isNarrow() ? onShowDay(day) : onCreate(day))}
              className={`flex min-h-16 min-w-0 sm:min-h-24 cursor-pointer flex-col gap-0.5 border-r border-b border-slate-200 p-1 [&:nth-child(7n)]:border-r-0 ${
                inMonth ? 'bg-white hover:bg-slate-50' : 'bg-slate-50/70 hover:bg-slate-100/70'
              }`}
            >
              <button
                type="button"
                onClick={(event) => {
                  event.stopPropagation()
                  onShowDay(day)
                }}
                aria-label={`${dayLabel.format(date)} – pokaż dzień`}
                aria-current={isToday ? 'date' : undefined}
                className={`mb-0.5 flex size-6 items-center justify-center self-center rounded-full text-xs tabular-nums hover:bg-slate-200 focus-visible:outline-2 focus-visible:outline-blue-600 ${
                  isToday ? 'bg-blue-600 font-semibold text-white hover:bg-blue-700' : inMonth ? 'text-slate-800' : 'text-slate-400'
                }`}
              >
                {date.getDate()}
              </button>
              {/* Telefon: kropki w kolorach elementów; szczegóły w widoku dnia. */}
              {dayItems.length > 0 && (
                <div className="flex flex-wrap justify-center gap-0.5 sm:hidden" aria-label={`Elementy: ${dayItems.length}`}>
                  {dayItems.slice(0, MAX_DOTS).map((item) => (
                    <span key={item.key} className="size-1.5 rounded-full" style={{ backgroundColor: itemColor(item) }} />
                  ))}
                </div>
              )}
              {dayItems.slice(0, hidden > 0 ? MAX_VISIBLE - 1 : MAX_VISIBLE).map((item) => (
                <div key={item.key} className="hidden sm:block">
                  <ItemChip item={item} onOpen={onOpenItem} />
                </div>
              ))}
              {hidden > 0 && (
                <button
                  type="button"
                  onClick={(event) => {
                    event.stopPropagation()
                    onShowDay(day)
                  }}
                  className="hidden rounded px-1.5 text-left text-xs font-medium text-slate-600 hover:bg-slate-200 sm:block"
                >
                  +{hidden + 1} więcej
                </button>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
