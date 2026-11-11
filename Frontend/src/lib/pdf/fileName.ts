/**
 * Nazwa pliku z tytułu: bez polskich znaków i znaków zabronionych w Windows, spacje jako myślniki,
 * np. „Granice ciągów: definicje” → „Granice-ciagow-definicje.pdf”. Pusty tytuł → nazwa zastępcza.
 */
export function toPdfFileName(title: string, fallback: string): string {
  // Znaki sterujące (np. tabulator) też są zabronione w nazwach plików.
  const printable = Array.from(title, (char) => (char.charCodeAt(0) < 32 ? ' ' : char)).join('')
  const base = printable
    .replace(/ł/g, 'l')
    .replace(/Ł/g, 'L')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '') // ą→a, ę→e, ó→o…
    .replace(/[<>:"/\\|?*]/g, ' ')
    .trim()
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^[-.]+|[-.]+$/g, '')
    .slice(0, 80)

  return `${base || fallback}.pdf`
}
