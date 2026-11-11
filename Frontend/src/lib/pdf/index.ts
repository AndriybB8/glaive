import type { TDocumentDefinitions } from 'pdfmake/interfaces'
import { loadPdfMake } from './loadPdfMake'

export { checkboxCanvas } from './checkbox'
export { toPdfFileName } from './fileName'
export { createPdfDocument, PDF_COLORS, PDF_CONTENT_WIDTH } from './pdfDocument'
export { progressBar } from './progressBar'

/** Generuje PDF w przeglądarce i pobiera go jako plik. Rzuca błąd, gdy biblioteki nie udało się wczytać albo wygenerować pliku. */
export async function downloadPdf(document: TDocumentDefinitions, fileName: string): Promise<void> {
  const pdfMake = await loadPdfMake()
  await pdfMake.createPdf(document).download(fileName)
}
