/** Ten sam format sprawdza backend (Tag.IsValidColor). */
const HEX_COLOR = /^#[0-9a-fA-F]{6}$/

export function isValidTagColor(color: string): boolean {
  return HEX_COLOR.test(color)
}

/** Gotowe kolory do wyboru jednym kliknięciem. Nazwy służą czytnikom ekranu i podpowiedziom. */
export const TAG_COLOR_PRESETS = [
  { value: '#DC2626', label: 'Czerwony' },
  { value: '#EA580C', label: 'Pomarańczowy' },
  { value: '#CA8A04', label: 'Żółty' },
  { value: '#16A34A', label: 'Zielony' },
  { value: '#0284C7', label: 'Błękitny' },
  { value: '#2563EB', label: 'Niebieski' },
  { value: '#7C3AED', label: 'Fioletowy' },
  { value: '#DB2777', label: 'Różowy' },
  { value: '#475569', label: 'Szary' },
] as const

export const DEFAULT_TAG_COLOR = '#2563EB'

/** Porównanie kolorów bez względu na wielkość liter (#2563eb to to samo co #2563EB). */
export function sameColor(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase()
}

/**
 * Kolor tekstu czytelny na tle tagu: biały albo prawie czarny, w zależności od jasności tła
 * (luminancja względna z WCAG). Dzięki temu użytkownik może wybrać dowolny kolor, także jasny.
 */
export function readableTextColor(background: string): string {
  if (!isValidTagColor(background)) return '#0f172a'

  const channel = (offset: number) => {
    const value = parseInt(background.slice(offset, offset + 2), 16) / 255
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
  }
  const luminance = 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5)

  // Kontrast z białym vs z czarnym – wybieramy większy.
  return (1.05 / (luminance + 0.05)) >= ((luminance + 0.05) / 0.05) ? '#ffffff' : '#0f172a'
}
