import { getToken } from '../../lib/authToken'
import { ApiError, request } from '../../lib/http'
import { contentTypeFor } from './fileRules'
import type { UploadedFile } from './types'

const BASE_URL = '/api/files'

/** Pliki bieżącego użytkownika, od najnowszych. */
export function getFiles(): Promise<UploadedFile[]> {
  return request<UploadedFile[]>(BASE_URL)
}

export function deleteFile(id: string): Promise<void> {
  return request(`${BASE_URL}/${id}`, { method: 'DELETE' })
}

export interface UploadProgress {
  loaded: number
  total: number
}

/** Wysyłanie przerwane przez użytkownika (przycisk „Anuluj” albo wyjście z zakładki). */
export class UploadAbortedError extends Error {
  constructor() {
    super('Wysyłanie anulowane.')
    this.name = 'UploadAbortedError'
  }
}

function problemMessage(responseText: string, status: number): string {
  try {
    const problem = JSON.parse(responseText) as { detail?: string; title?: string }
    if (problem.detail ?? problem.title) return (problem.detail ?? problem.title)!
  } catch {
    // Odpowiedź bez JSON-a (np. 413 z serwera przed kontrolerem) – komunikat ogólny poniżej.
  }
  if (status === 413) return 'Plik jest za duży.'
  return `Błąd serwera (${status}).`
}

/**
 * Wysyła plik (POST /api/files, multipart/form-data, pole `file`) i raportuje postęp.
 * Przez XMLHttpRequest, bo `fetch` nie podaje postępu wysyłania. Błędy jak w `request`: {@link ApiError} z komunikatem serwera,
 * a przerwanie przez `signal` – {@link UploadAbortedError}.
 * Ten sam plik (ta sama treść) wysłany drugi raz nie tworzy kopii – serwer zwraca istniejący wpis.
 */
export function uploadFile(file: File, onProgress: (progress: UploadProgress) => void, signal?: AbortSignal): Promise<UploadedFile> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new UploadAbortedError())
      return
    }

    // Typ ustawiony po rozszerzeniu (patrz contentTypeFor) – backend przyjmuje plik po typie z formularza.
    const type = contentTypeFor(file.name) ?? file.type
    const form = new FormData()
    form.append('file', new File([file], file.name, { type }))

    const xhr = new XMLHttpRequest()
    xhr.open('POST', BASE_URL)
    const token = getToken()
    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`)

    xhr.upload.onprogress = (event) => {
      onProgress({ loaded: event.loaded, total: event.lengthComputable ? event.total : file.size })
    }
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        onProgress({ loaded: file.size, total: file.size })
        resolve(JSON.parse(xhr.responseText) as UploadedFile)
      } else {
        reject(new ApiError(xhr.status, problemMessage(xhr.responseText, xhr.status)))
      }
    }
    xhr.onerror = () => reject(new ApiError(0, 'Brak połączenia z serwerem. Sprawdź, czy backend jest uruchomiony.'))
    xhr.onabort = () => reject(new UploadAbortedError())

    const abort = () => xhr.abort()
    signal?.addEventListener('abort', abort, { once: true })
    xhr.onloadend = () => signal?.removeEventListener('abort', abort)

    xhr.send(form)
  })
}

/**
 * Pobiera plik na dysk (GET /api/files/{id}/content). Zwykły link nie wystarczy – zapytanie musi mieć nagłówek z tokenem,
 * więc treść pobieramy jako blob i zapisujemy przez tymczasowy adres.
 */
export async function downloadFile(file: UploadedFile): Promise<void> {
  const token = getToken()
  const response = await fetch(`${BASE_URL}/${file.id}/content`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!response.ok) {
    throw new ApiError(response.status, problemMessage(await response.text(), response.status))
  }

  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = file.originalFileName
  document.body.append(link)
  link.click()
  link.remove()
  // Chwila zwłoki – część przeglądarek zaczyna zapis dopiero po zakończeniu kliknięcia.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
