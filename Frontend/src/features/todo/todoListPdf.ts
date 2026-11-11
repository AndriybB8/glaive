import type { TableCell, TDocumentDefinitions } from 'pdfmake/interfaces'
import { checkboxCanvas, createPdfDocument, PDF_COLORS, progressBar } from '../../lib/pdf'
import { ACCESS_LABELS } from './sharingApi'
import type { TodoItem, TodoListSummary } from './types'

/** Rozmiar pola wyboru – większy niż w notatce, żeby na wydruku dało się je odhaczyć długopisem. */
const CHECKBOX_SIZE = 11

function itemRow(item: TodoItem): TableCell[] {
  return [
    { canvas: checkboxCanvas(item.isDone, CHECKBOX_SIZE) },
    {
      text: item.title,
      fontSize: 11,
      ...(item.isDone ? { color: PDF_COLORS.muted, decoration: 'lineThrough' as const } : {}),
    },
  ]
}

interface TodoListPdfOptions {
  list: TodoListSummary
  /** Punkty w kolejności z ekranu. */
  items: TodoItem[]
}

/**
 * Lista do wydruku: nazwa, postęp i punkty z polami wyboru, z odstępami jak na kartce – zrobione zaznaczone
 * i przekreślone, pozostałe z pustą ramką do odhaczenia. Przy liście udostępnionej – Twoje uprawnienia
 * (adresu właściciela API nie podaje osobom, którym udostępniono listę).
 */
export function createTodoListPdf({ list, items }: TodoListPdfOptions): TDocumentDefinitions {
  const done = items.filter((i) => i.isDone).length
  const subtitle = [
    items.length === 0 ? 'Brak punktów' : `Zrobione: ${done} z ${items.length}`,
    list.access !== 'owner' && `Lista udostępniona · Twoje uprawnienia: ${ACCESS_LABELS[list.access]}`,
  ]
    .filter(Boolean)
    .join(' · ')

  return createPdfDocument({
    title: list.title,
    subtitle,
    content:
      items.length === 0
        ? [{ text: 'Lista jest pusta.', color: PDF_COLORS.muted, italics: true }]
        : [
            progressBar(done, items.length),
            {
              table: {
                // Punkt nie dzieli się między strony.
                dontBreakRows: true,
                widths: [CHECKBOX_SIZE + 6, '*'],
                body: items.map(itemRow),
              },
              layout: {
                hLineWidth: (i, node) => (i === 0 || i === node.table.body.length ? 0 : 0.5),
                vLineWidth: () => 0,
                hLineColor: () => PDF_COLORS.border,
                paddingLeft: (i) => (i === 0 ? 0 : 6),
                paddingRight: () => 0,
                paddingTop: () => 7,
                paddingBottom: () => 7,
              },
            },
          ],
  })
}
