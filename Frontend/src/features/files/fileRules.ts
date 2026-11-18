/**
 * Zasady przyjmowania plików – te same co na backendzie (Domain/Files/UploadedFile.cs: MaxSizeBytes, MaxFileNameLength,
 * AllowedContentTypes). Przeglądarka sprawdza je przed wysłaniem, żeby nie wysyłać na próżno; serwer i tak sprawdza je ponownie.
 * Zakres zadania (SCRUM-118): PDF, DOCX i TXT – z nich powstaną fiszki.
 */

/** 5 MB – UploadedFile.MaxSizeBytes. */
export const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024

/** UploadedFile.MaxFileNameLength. */
export const MAX_FILE_NAME_LENGTH = 255

/**
 * Typ MIME po rozszerzeniu. Backend sprawdza typ z nagłówka części formularza, a przeglądarki (zwłaszcza na Windows)
 * potrafią wysłać `.docx` jako pusty typ albo `application/octet-stream` – dlatego typ ustawiamy sami.
 */
const CONTENT_TYPES: Record<string, string> = {
  pdf: 'application/pdf',
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  txt: 'text/plain',
}

/** Wartość atrybutu `accept` pola wyboru pliku. */
export const ACCEPT_ATTRIBUTE = Object.entries(CONTENT_TYPES)
  .flatMap(([extension, type]) => [`.${extension}`, type])
  .join(',')

export const ACCEPTED_LABEL = 'PDF, DOCX lub TXT'

function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.')
  return dot === -1 ? '' : name.slice(dot + 1).toLowerCase()
}

/** Typ MIME, z jakim plik trafi na serwer, albo `null`, gdy rozszerzenie nie jest obsługiwane. */
export function contentTypeFor(name: string): string | null {
  return CONTENT_TYPES[extensionOf(name)] ?? null
}

/** Komunikat dla użytkownika, gdy pliku nie da się wysłać, albo `null`, gdy wszystko w porządku. */
export function validateFile(file: Pick<File, 'name' | 'size'>): string | null {
  if (contentTypeFor(file.name) === null) {
    return `Nieobsługiwany typ pliku. Wybierz plik ${ACCEPTED_LABEL}.`
  }
  if (file.size === 0) {
    return 'Plik jest pusty.'
  }
  if (file.size > MAX_FILE_SIZE_BYTES) {
    return `Plik jest za duży (${formatFileSize(file.size)}). Maksymalnie ${formatFileSize(MAX_FILE_SIZE_BYTES)}.`
  }
  if (file.name.length > MAX_FILE_NAME_LENGTH) {
    return `Nazwa pliku może mieć maksymalnie ${MAX_FILE_NAME_LENGTH} znaków.`
  }
  return null
}

/** Rozmiar czytelny dla człowieka: „820 B”, „14 KB”, „2,4 MB”. */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toLocaleString('pl-PL', { maximumFractionDigits: 1 })} MB`
}

/** Krótka etykieta typu do listy plików. */
export function fileKindLabel(name: string): string {
  return extensionOf(name).toUpperCase() || 'PLIK'
}
