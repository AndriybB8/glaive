import { useId, useState } from 'react'
import type { HistoryEntry } from './changeHistory'
import { describeScheduleChange } from './describeScheduleChange'

/** Tyle wpisów widać od razu; resztę po „Pokaż wszystkie”. */
const COLLAPSED_COUNT = 5

const timeFormat = new Intl.DateTimeFormat('pl-PL', { hour: '2-digit', minute: '2-digit', second: '2-digit' })

interface ScheduleHistoryProps {
  entries: readonly HistoryEntry[]
  meId: string | null
}

/**
 * Historia zmian harmonogramu – kto co zmienił, z godziną, najnowsze na górze. Także własne zmiany (z dopiskiem „Ty”),
 * żeby było widać pełny przebieg pracy. Backend nie przechowuje historii, więc to zmiany odebrane na żywo,
 * odkąd zakładka „Harmonogramy” jest otwarta – i panel mówi to wprost.
 */
export function ScheduleHistory({ entries, meId }: ScheduleHistoryProps) {
  const headingId = useId()
  const [expanded, setExpanded] = useState(false)
  const visible = expanded ? entries : entries.slice(0, COLLAPSED_COUNT)

  return (
    <section aria-labelledby={headingId} className="mt-8 border-t border-slate-200 pt-5">
      <h2 id={headingId} className="text-sm font-medium text-slate-700">
        Historia zmian
      </h2>
      <p className="mt-0.5 text-xs text-slate-500">Zmiany od otwarcia zakładki „Harmonogramy”. Po odświeżeniu strony historia zaczyna się od nowa.</p>

      {entries.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">Na razie bez zmian.</p>
      ) : (
        <>
          <ol className="mt-3 flex flex-col gap-2">
            {visible.map(({ id, message }) => {
              const mine = meId !== null && message.actorUserId === meId
              return (
                <li key={id} className="flex items-baseline gap-3 text-sm">
                  <time dateTime={message.occurredAt} className="w-16 shrink-0 text-xs text-slate-500 tabular-nums">
                    {timeFormat.format(new Date(message.occurredAt))}
                  </time>
                  <p className="min-w-0 flex-1 break-words text-slate-700">
                    <span className="font-medium text-slate-900">{message.actorEmail || 'Ktoś'}</span>
                    {mine && <span className="text-slate-500"> (Ty)</span>}{' '}
                    {describeScheduleChange(message, meId)}
                  </p>
                </li>
              )
            })}
          </ol>
          {entries.length > COLLAPSED_COUNT && (
            <button
              type="button"
              onClick={() => setExpanded((current) => !current)}
              aria-expanded={expanded}
              className="mt-2 text-sm font-medium text-blue-700 hover:underline"
            >
              {expanded ? 'Pokaż mniej' : `Pokaż wszystkie (${entries.length})`}
            </button>
          )}
        </>
      )}
    </section>
  )
}
