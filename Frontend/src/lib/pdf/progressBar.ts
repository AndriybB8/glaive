import type { Content } from 'pdfmake/interfaces'
import { PDF_COLORS, PDF_CONTENT_WIDTH } from './pdfDocument'

const PROGRESS_COLOR = '#2563eb'

/** Pasek postępu jak w aplikacji (ProgressBar): szare tło na całą szerokość i niebieskie wypełnienie. */
export function progressBar(done: number, total: number): Content {
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
