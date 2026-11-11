import type { Content, TDocumentDefinitions } from 'pdfmake/interfaces'

/** Kolory jak w aplikacji (Tailwind slate/blue), żeby PDF wyglądał „swojo”. */
export const PDF_COLORS = {
  text: '#0f172a',
  muted: '#64748b',
  border: '#cbd5e1',
  subtle: '#f1f5f9',
  link: '#1d4ed8',
} as const

/** A4 (595 × 842 pt) z marginesami 48 pt – szerokość treści przydaje się do linii i tabel. */
export const PDF_CONTENT_WIDTH = 595 - 2 * 48

const generatedAtFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'medium', timeStyle: 'short' })

interface PdfDocumentOptions {
  /** Tytuł dokumentu – nagłówek pierwszej strony i metadane pliku. */
  title: string
  /** Linia pod tytułem, np. data zmiany i grupa. */
  subtitle?: string
  content: Content[]
}

/**
 * Wspólny wygląd wszystkich eksportów: A4, tytuł z podtytułem, stopka „StudyPlanner · data wygenerowania”
 * i numer strony. Eksport notatki, harmonogramu i listy podaje tylko treść.
 */
export function createPdfDocument({ title, subtitle, content }: PdfDocumentOptions): TDocumentDefinitions {
  const generatedAt = generatedAtFormat.format(new Date())

  return {
    pageSize: 'A4',
    pageMargins: [48, 48, 48, 56],
    info: { title, creator: 'StudyPlanner', producer: 'StudyPlanner (pdfmake)' },
    defaultStyle: { font: 'Roboto', fontSize: 10.5, lineHeight: 1.3, color: PDF_COLORS.text },
    styles: {
      documentTitle: { fontSize: 20, bold: true, margin: [0, 0, 0, 4] },
      documentSubtitle: { fontSize: 9.5, color: PDF_COLORS.muted, margin: [0, 0, 0, 16] },
    },
    content: [
      { text: title, style: 'documentTitle' },
      subtitle ? { text: subtitle, style: 'documentSubtitle' } : { text: '', margin: [0, 0, 0, 12] },
      ...content,
    ],
    footer: (currentPage, pageCount) => ({
      margin: [48, 16, 48, 0],
      fontSize: 8.5,
      color: PDF_COLORS.muted,
      columns: [
        { text: `StudyPlanner · wygenerowano ${generatedAt}` },
        { text: `Strona ${currentPage} z ${pageCount}`, alignment: 'right' },
      ],
    }),
  }
}
