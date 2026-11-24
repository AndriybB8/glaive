import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ACCESS_LABELS } from '../sharing/sharing'
import { priorityOptions, statusOptions } from '../tasks/taskMeta'
import { fromIsoDate } from './dates'
import type { CalendarItem } from './types'

const dateFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'long', year: 'numeric' })
const format = (iso: string) => dateFormat.format(fromIsoDate(iso))

/** Harmonogram albo zadanie w kalendarzu – tylko podgląd; zmienia się je w zakładce „Harmonogramy”. */
export function ItemDetailsDialog({ item, onClose }: { item: CalendarItem; onClose: () => void }) {
  const rows: [string, string][] =
    item.kind === 'schedule'
      ? [
          ['Okres', `${format(item.startDate!)} – ${format(item.endDate!)}`],
          ['Twoja rola', ACCESS_LABELS[item.access]],
        ]
      : [
          ['Termin', format(item.startDate!)],
          ['Harmonogram', item.scheduleName ?? '—'],
          ['Status', statusOptions.find((o) => o.value === item.taskStatus)?.label ?? '—'],
          ['Priorytet', priorityOptions.find((o) => o.value === item.taskPriority)?.label ?? '—'],
        ]

  return (
    <Dialog
      title={item.title}
      description={item.kind === 'schedule' ? 'Harmonogram' : 'Zadanie'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} className="min-h-11 sm:min-h-0">
            Zamknij
          </Button>
          <a
            href="#harmonogramy"
            className="inline-flex min-h-11 items-center justify-center rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 sm:min-h-0"
            data-autofocus
          >
            Przejdź do harmonogramów
          </a>
        </>
      }
    >
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-slate-500">{label}</dt>
            <dd className="text-slate-900">{value}</dd>
          </div>
        ))}
      </dl>
      {item.description && <p className="text-sm whitespace-pre-line text-slate-700">{item.description}</p>}
    </Dialog>
  )
}
