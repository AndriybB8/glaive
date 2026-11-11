import type { JSONContent } from '@tiptap/react'
import type { Content, ContentText, Decoration } from 'pdfmake/interfaces'
import { checkboxCanvas, createPdfDocument, PDF_COLORS, PDF_CONTENT_WIDTH } from '../../lib/pdf'
import type { TDocumentDefinitions } from 'pdfmake/interfaces'

/**
 * Zamiana treści notatki (dokument TipTapa, editor.getJSON()) na elementy pdfmake.
 * Obsługuje to, co daje edytor (noteExtensions.ts): akapity, nagłówki 2–3, pogrubienie, kursywę, podkreślenie,
 * przekreślenie, kod, linki, listy punktowane, numerowane i zadań (z polami wyboru), cytat, blok kodu, linię.
 * Nieznane węzły (np. z przyszłych rozszerzeń) nie psują eksportu – trafia z nich sam tekst.
 */

/** Odstęp pod akapitem; w listach mniejszy, żeby punkty nie „rozjeżdżały się”. */
const BLOCK_GAP = 6
const LIST_GAP = 2

interface Context {
  /** Wewnątrz punktu listy – mniejsze odstępy. */
  inList: boolean
}

function inline(nodes: JSONContent[] = []): ContentText[] {
  return nodes.flatMap((node): ContentText[] => {
    if (node.type === 'hardBreak') return [{ text: '\n' }]
    if (node.type !== 'text' || !node.text) return inline(node.content)

    const run: ContentText = { text: node.text }
    const decoration: Decoration[] = []
    for (const mark of node.marks ?? []) {
      switch (mark.type) {
        case 'bold':
          run.bold = true
          break
        case 'italic':
          run.italics = true
          break
        case 'underline':
          decoration.push('underline')
          break
        case 'strike':
          decoration.push('lineThrough')
          break
        case 'code':
          run.background = PDF_COLORS.subtle
          run.fontSize = 9.5
          break
        case 'link':
          if (typeof mark.attrs?.href === 'string') {
            run.link = mark.attrs.href
            run.color = PDF_COLORS.link
            decoration.push('underline')
          }
          break
      }
    }
    if (decoration.length > 0) run.decoration = decoration.length === 1 ? decoration[0] : decoration
    return [run]
  })
}

/** Sam tekst węzła – do nieznanych węzłów i bloku kodu. */
function plainText(node: JSONContent): string {
  if (node.type === 'text') return node.text ?? ''
  if (node.type === 'hardBreak') return '\n'
  return (node.content ?? []).map(plainText).join('')
}

function paragraph(node: JSONContent, ctx: Context, extra: Partial<ContentText> = {}): Content {
  const runs = inline(node.content)
  // Pusty akapit = pusta linia, jak w edytorze.
  return { text: runs.length > 0 ? runs : ' ', margin: [0, 0, 0, ctx.inList ? LIST_GAP : BLOCK_GAP], ...extra }
}

/** Zawartość punktu listy: jeden akapit jako zwykły tekst, kilka bloków (np. lista zagnieżdżona) jako stos. */
function listItemContent(node: JSONContent): Content {
  const children = blocks(node.content, { inList: true })
  return children.length === 1 ? children[0] : { stack: children }
}

function taskItem(node: JSONContent): Content {
  const checked = node.attrs?.checked === true
  const children = blocks(node.content, { inList: true })
  return {
    columns: [
      { width: 14, canvas: checkboxCanvas(checked) },
      // Zrobione – szare i przekreślone, jak w edytorze.
      { width: '*', stack: children, ...(checked ? { color: PDF_COLORS.muted, decoration: 'lineThrough' as const } : {}) },
    ],
    columnGap: 2,
  }
}

function block(node: JSONContent, ctx: Context): Content | null {
  const gap = ctx.inList ? LIST_GAP : BLOCK_GAP

  switch (node.type) {
    case 'paragraph':
      return paragraph(node, ctx)

    case 'heading': {
      const level = node.attrs?.level === 3 ? 3 : 2
      return paragraph(node, ctx, {
        bold: true,
        fontSize: level === 2 ? 15 : 12.5,
        margin: [0, level === 2 ? 10 : 8, 0, 4],
      })
    }

    case 'bulletList':
      return { ul: (node.content ?? []).map(listItemContent), margin: [0, 0, 0, gap] }

    case 'orderedList': {
      const start = typeof node.attrs?.start === 'number' ? node.attrs.start : 1
      return { ol: (node.content ?? []).map(listItemContent), start, margin: [0, 0, 0, gap] }
    }

    case 'taskList':
      return { stack: (node.content ?? []).map(taskItem), margin: [0, 0, 0, gap] }

    case 'blockquote':
      // Tabela z jedną komórką i samą lewą krawędzią – pionowa kreska jak w edytorze.
      return {
        table: { widths: ['*'], body: [[{ stack: blocks(node.content, ctx), border: [true, false, false, false] }]] },
        layout: {
          vLineWidth: () => 2,
          vLineColor: () => PDF_COLORS.border,
          hLineWidth: () => 0,
          paddingLeft: () => 10,
          paddingTop: () => 0,
          paddingBottom: () => 0,
        },
        color: PDF_COLORS.muted,
        margin: [0, 0, 0, gap],
      }

    case 'codeBlock':
      return {
        table: { widths: ['*'], body: [[{ text: plainText(node), preserveLeadingSpaces: true, fontSize: 9.5 }]] },
        layout: {
          fillColor: () => PDF_COLORS.subtle,
          hLineWidth: () => 0,
          vLineWidth: () => 0,
          paddingLeft: () => 8,
          paddingRight: () => 8,
          paddingTop: () => 6,
          paddingBottom: () => 6,
        },
        margin: [0, 0, 0, gap],
      }

    case 'horizontalRule':
      return {
        canvas: [{ type: 'line', x1: 0, y1: 0, x2: PDF_CONTENT_WIDTH, y2: 0, lineWidth: 0.75, lineColor: PDF_COLORS.border }],
        margin: [0, 6, 0, 12],
      }

    default: {
      const text = plainText(node).trim()
      return text ? { text, margin: [0, 0, 0, gap] } : null
    }
  }
}

function blocks(nodes: JSONContent[] = [], ctx: Context = { inList: false }): Content[] {
  return nodes.map((node) => block(node, ctx)).filter((content): content is Content => content !== null)
}

const updatedAtFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'long', timeStyle: 'short' })

interface NotePdfOptions {
  title: string
  /** Treść z edytora (editor.getJSON()) – razem z niezapisanymi zmianami. */
  doc: JSONContent
  updatedAt: string
  groupName: string | null
}

export function createNotePdf({ title, doc, updatedAt, groupName }: NotePdfOptions): TDocumentDefinitions {
  const subtitle = [`Ostatnia zmiana: ${updatedAtFormat.format(new Date(updatedAt))}`, groupName && `Grupa: ${groupName}`]
    .filter(Boolean)
    .join(' · ')
  const content = blocks(doc.content)

  return createPdfDocument({
    title: title.trim() || 'Bez tytułu',
    subtitle,
    content: content.length > 0 ? content : [{ text: 'Ta notatka jest pusta.', color: PDF_COLORS.muted, italics: true }],
  })
}
