import type { Content, ContentText, TableCell, TDocumentDefinitions } from 'pdfmake/interfaces'
import { createPdfDocument, PDF_COLORS, PDF_CONTENT_WIDTH } from '../../lib/pdf'
import { countDays, formatDate, formatDateRange, formatDays } from '../schedules/scheduleDates'
import type { Schedule } from '../schedules/types'
import { readableTextColor } from '../tags/tagColors'
import type { Tag } from '../tags/types'
import { groupTasks, isOverdue, priorityOptions } from './taskMeta'
import type { Task } from './types'

const OVERDUE_COLOR = '#b91c1c'
const PROGRESS_COLOR = '#2563eb'

const priorityLabels = Object.fromEntries(priorityOptions.map((o) => [o.value, o.label])) as Record<Task['priority'], string>

/** Pasek postępu narysowany jak w aplikacji: szare tło i niebieskie wypełnienie. */
function progressBar(done: number, total: number): Content {
  const height = 6
  const filled = total > 0 ? (done / total) * PDF_CONTENT_WIDTH : 0
  return {
    canvas: [
      { type: 'rect', x: 0, y: 0, w: PDF_CONTENT_WIDTH, h: height, r: 3, color: PDF_COLORS.subtle },
      ...(filled > 0 ? [{ type: 'rect' as const, x: 0, y: 0, w: filled, h: height, r: 3, color: PROGRESS_COLOR }] : []),
    ],
    margin: [0, 4, 0, 14],
  }
}

/** Tagi jako kolorowe etykiety w jednej linii (tło w kolorze tagu, czytelny tekst – jak TagChip). */
function tagLine(tags: readonly Tag[]): Content {
  const runs: ContentText[] = tags.flatMap((tag, index) => [
    ...(index > 0 ? [{ text: '  ' }] : []),
    { text: `\u00a0${tag.name}\u00a0`, background: tag.color, color: readableTextColor(tag.color), fontSize: 8, bold: true },
  ])
  return { text: runs, margin: [0, 3, 0, 0] }
}

function taskCell(task: Task, tags: readonly Tag[]): TableCell {
  const done = task.status === 'done'
  const stack: Content[] = [
    { text: task.title, bold: true, ...(done ? { color: PDF_COLORS.muted, decoration: 'lineThrough' as const } : {}) },
  ]
  if (task.description) stack.push({ text: task.description, fontSize: 9, color: PDF_COLORS.muted, margin: [0, 2, 0, 0] })
  if (tags.length > 0) stack.push(tagLine(tags))
  return { stack }
}

function dueCell(task: Task, today: string): TableCell {
  if (task.dueDate === null) return { text: 'Bez terminu', color: PDF_COLORS.muted }
  if (isOverdue(task, today)) {
    return { stack: [{ text: formatDate(task.dueDate) }, { text: 'Po terminie', fontSize: 8.5, bold: true, color: OVERDUE_COLOR }] }
  }
  return { text: formatDate(task.dueDate) }
}

/**
 * Tabela jednej grupy (np. „W trakcie”). Nazwa grupy jest pierwszym wierszem nagłówka: pdfmake powtarza nagłówek
 * na każdej stronie i trzyma go z pierwszym zadaniem (keepWithHeaderRows), więc nazwa grupy nigdy nie zostaje
 * sama na dole strony, a na kolejnej stronie wiadomo, której grupy to ciąg dalszy.
 */
function groupTable(label: string, tasks: Task[], tagsByTask: Readonly<Record<string, Tag[]>>, today: string): Content {
  const header = (text: string): TableCell => ({ text, bold: true, fontSize: 9, color: PDF_COLORS.muted })
  return {
    table: {
      headerRows: 2,
      keepWithHeaderRows: 1,
      // Zadanie nie dzieli się między strony – wiersz przechodzi w całości na następną.
      dontBreakRows: true,
      widths: ['*', 78, 52],
      body: [
        [
          {
            text: [label, { text: ` (${tasks.length})`, color: PDF_COLORS.muted, bold: false }],
            bold: true,
            fontSize: 12,
            colSpan: 3,
            margin: [0, 0, 0, 2],
          },
          {},
          {},
        ],
        [header('Zadanie'), header('Termin'), header('Priorytet')],
        ...tasks.map((task): TableCell[] => [taskCell(task, tagsByTask[task.id] ?? []), dueCell(task, today), { text: priorityLabels[task.priority] }]),
      ],
    },
    layout: {
      // Linie: brak nad nazwą grupy i pod nią, wyraźniejsza pod nagłówkami kolumn, cienkie między zadaniami.
      hLineWidth: (i, node) => (i <= 1 || i === node.table.body.length ? 0 : i === 2 ? 0.9 : 0.5),
      vLineWidth: () => 0,
      hLineColor: () => PDF_COLORS.border,
      paddingLeft: (i) => (i === 0 ? 0 : 8),
      paddingRight: () => 0,
      paddingTop: () => 6,
      paddingBottom: () => 6,
    },
    margin: [0, 0, 0, 14],
  }
}

interface SchedulePdfOptions {
  schedule: Schedule
  /** Wszystkie zadania harmonogramu – do postępu (jak na ekranie: filtr go nie zmienia). */
  allTasks: Task[]
  /** Zadania widoczne na liście, czyli po filtrze tagów. */
  visibleTasks: Task[]
  tagsByTask: Readonly<Record<string, Tag[]>>
  /** Tagi aktywnego filtru (puste = bez filtru). */
  filterTags: readonly Tag[]
  today: string
}

/** Harmonogram jak na ekranie: daty, postęp, zadania pogrupowane wg statusu, z terminem, priorytetem i tagami. */
export function createSchedulePdf({ schedule, allTasks, visibleTasks, tagsByTask, filterTags, today }: SchedulePdfOptions): TDocumentDefinitions {
  const done = allTasks.filter((t) => t.status === 'done').length
  const total = allTasks.length
  const progressText =
    total === 0
      ? 'Brak zadań'
      : done === total
        ? `Wszystkie zadania ukończone · ${total}`
        : `Ukończone: ${done} z ${total} · ${Math.round((done / total) * 100)}%`

  const content: Content[] = [{ text: progressText, fontSize: 10 }, progressBar(done, total)]

  if (filterTags.length > 0) {
    const names = filterTags.map((t) => `„${t.name}”`).join(', ')
    content.push({
      text: [
        { text: 'Filtr: ', bold: true },
        filterTags.length === 1 ? `zadania z tagiem ${names}` : `zadania ze wszystkimi tagami: ${names}`,
        ` · pasujące: ${visibleTasks.length} z ${total}`,
      ],
      fontSize: 9.5,
      color: PDF_COLORS.muted,
      margin: [0, -4, 0, 14],
    })
  }

  const groups = groupTasks(visibleTasks)
  if (groups.length === 0) {
    content.push({
      text: total === 0 ? 'Ten harmonogram nie ma jeszcze zadań.' : 'Żadne zadanie nie pasuje do filtru.',
      color: PDF_COLORS.muted,
      italics: true,
    })
  }
  for (const group of groups) content.push(groupTable(group.label, group.tasks, tagsByTask, today))

  return createPdfDocument({
    title: schedule.name,
    subtitle: `${formatDateRange(schedule.startDate, schedule.endDate)} · ${formatDays(countDays(schedule.startDate, schedule.endDate))}`,
    content,
  })
}
