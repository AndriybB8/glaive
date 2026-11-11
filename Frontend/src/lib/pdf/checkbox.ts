import type { CanvasElement } from 'pdfmake/interfaces'
import { PDF_COLORS } from './pdfDocument'

/**
 * Pole wyboru narysowane wektorowo (Roboto nie ma znaków ☐/☑). Zaznaczone – wypełnione z ✓,
 * puste – sama ramka, którą da się odhaczyć długopisem na wydruku.
 */
export function checkboxCanvas(checked: boolean, size = 9): CanvasElement[] {
  const box: CanvasElement = {
    type: 'rect',
    x: 0,
    y: 2,
    w: size,
    h: size,
    r: 1.5,
    lineWidth: 0.9,
    lineColor: checked ? PDF_COLORS.text : PDF_COLORS.muted,
    ...(checked ? { color: PDF_COLORS.text } : {}),
  }
  if (!checked) return [box]

  const tick: CanvasElement = {
    type: 'polyline',
    lineWidth: 1.3,
    lineColor: '#ffffff',
    points: [
      { x: size * 0.22, y: 2 + size * 0.52 },
      { x: size * 0.42, y: 2 + size * 0.72 },
      { x: size * 0.78, y: 2 + size * 0.28 },
    ],
  }
  return [box, tick]
}
