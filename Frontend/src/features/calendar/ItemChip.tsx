import { readableTextColor } from '../tags/tagColors'
import { formatTime } from './dates'
import { describeItem } from './describeItem'
import { itemColor } from './layout'
import type { CalendarItem } from './types'

interface ItemChipProps {
  item: CalendarItem
  onOpen: (item: CalendarItem) => void
}

/** Ikona powtarzania – przy wystąpieniach serii. */
function RepeatMark() {
  return (
    <svg viewBox="0 0 16 16" className="size-3 shrink-0" fill="none" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M3 7a5 5 0 0 1 8.5-3.5L13 5" />
      <path d="M13 2v3h-3" />
      <path d="M13 9a5 5 0 0 1-8.5 3.5L3 11" />
      <path d="M3 14v-3h3" />
    </svg>
  )
}

/**
 * Element w jednej linii (widok miesiąca i pasek całodniowy). Całodniowy – pełne tło w swoim kolorze,
 * w godzinach – kropka i godzina, jak w Google Calendar. Harmonogram trwa zwykle tygodniami, więc ma jasne tło
 * z paskiem z boku – jest tłem dla zadań i wydarzeń, a nie zasłania ich. Zrobione zadania są przekreślone.
 */
export function ItemChip({ item, onOpen }: ItemChipProps) {
  const color = itemColor(item)
  const done = item.kind === 'task' && item.taskStatus === 'done'
  const label = describeItem(item)

  return (
    <button
      type="button"
      onClick={(event) => {
        event.stopPropagation()
        onOpen(item)
      }}
      title={label}
      aria-label={label}
      className="flex w-full min-w-0 items-center gap-1 rounded px-1.5 py-0.5 text-left text-xs leading-5 hover:brightness-95 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-blue-600"
      style={
        item.kind === 'schedule'
          ? { backgroundColor: `${color}1f`, boxShadow: `inset 3px 0 0 ${color}`, color: '#134e4a' }
          : item.isAllDay
            ? { backgroundColor: color, color: readableTextColor(color) }
            : undefined
      }
    >
      {!item.isAllDay && <span aria-hidden className="size-2 shrink-0 rounded-full" style={{ backgroundColor: color }} />}
      {!item.isAllDay && <span className="shrink-0 text-slate-500 tabular-nums">{formatTime(item.startsAt!)}</span>}
      <span className={`min-w-0 truncate ${item.isAllDay ? 'font-medium' : 'text-slate-800'} ${done ? 'line-through opacity-80' : ''}`}>
        {item.title}
      </span>
      {item.isRecurring && <RepeatMark />}
    </button>
  )
}
