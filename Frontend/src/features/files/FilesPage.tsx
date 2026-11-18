import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { IconButton } from '../../components/ui/IconButton'
import { DownloadIcon, TrashIcon } from '../../components/ui/icons'
import { ApiError } from '../../lib/http'
import { formatUpdatedAt } from '../notes/formatDate'
import { downloadFile, getFiles, uploadFile, UploadAbortedError } from './api'
import { DeleteFileDialog } from './DeleteFileDialog'
import { FileDropZone } from './FileDropZone'
import { fileKindLabel, formatFileSize, validateFile } from './fileRules'
import type { UploadedFile } from './types'
import { UploadQueue, type UploadItem } from './UploadQueue'

const LOAD_ERROR = 'Nie udało się wczytać plików. Sprawdź, czy backend jest uruchomiony.'

/** Błąd wysyłania → komunikat i czy ponowienie ma sens (nie ma, gdy serwer odrzucił sam plik: typ, rozmiar, nazwa). */
function describeUploadError(error: unknown): { message: string; retryable: boolean } {
  if (error instanceof UploadAbortedError) return { message: 'Wysyłanie anulowane.', retryable: true }
  if (error instanceof ApiError) {
    const rejected = error.status === 400 || error.status === 413
    return { message: error.message, retryable: !rejected }
  }
  return { message: 'Nie udało się wysłać pliku.', retryable: true }
}

/**
 * Zakładka „Pliki” (SCRUM-118): wysyłanie materiałów PDF, DOCX i TXT z paskiem postępu oraz lista własnych plików
 * (pobieranie, usuwanie). Z tych plików w kolejnych zadaniach powstaną fiszki (SCRUM-121, SCRUM-122).
 */
export function FilesPage() {
  const [files, setFiles] = useState<UploadedFile[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [uploads, setUploads] = useState<UploadItem[]>([])
  const [toDelete, setToDelete] = useState<UploadedFile | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  /** Komunikat dla czytników ekranu po udanym wysłaniu (pasek postępu znika razem z pozycją). */
  const [announcement, setAnnouncement] = useState('')

  const controllers = useRef(new Map<string, AbortController>())
  const nextKey = useRef(0)
  // Aktualna lista dla kodu po `await` – żeby rozpoznać plik, który już był (serwer nie tworzy kopii tej samej treści).
  const filesRef = useRef<UploadedFile[] | null>(null)
  useEffect(() => {
    filesRef.current = files
  }, [files])

  const loadFiles = useCallback(async () => {
    try {
      setListError(null)
      setFiles(await getFiles())
    } catch {
      setListError(LOAD_ERROR)
    }
  }, [])

  useEffect(() => {
    let ignore = false
    getFiles()
      .then((result) => {
        if (!ignore) setFiles(result)
      })
      .catch(() => {
        if (!ignore) setListError(LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [])

  // Wyjście z zakładki przerywa trwające wysyłanie (bez tego plik i tak by doszedł, ale bez paska postępu i komunikatu).
  useEffect(() => {
    const active = controllers.current
    return () => {
      active.forEach((controller) => controller.abort())
      active.clear()
    }
  }, [])

  const updateUpload = (key: string, change: Partial<UploadItem>) =>
    setUploads((current) => current.map((item) => (item.key === key ? { ...item, ...change } : item)))

  const removeUpload = (key: string) => setUploads((current) => current.filter((item) => item.key !== key))

  const startUpload = async (key: string, file: File) => {
    const controller = new AbortController()
    controllers.current.set(key, controller)
    try {
      const saved = await uploadFile(file, ({ loaded, total }) => updateUpload(key, { loaded, total }), controller.signal)
      const existing = filesRef.current?.find((f) => f.id === saved.id)
      if (existing) {
        updateUpload(key, { status: 'duplicate', message: `Masz już ten plik („${existing.originalFileName}”) – nie dodano kopii.` })
        return
      }
      removeUpload(key)
      setFiles((current) => [saved, ...(current ?? []).filter((f) => f.id !== saved.id)])
      setAnnouncement(`Wysłano plik „${saved.originalFileName}”.`)
    } catch (error) {
      if (controller.signal.aborted && !controllers.current.has(key)) return // zakładka zamknięta
      updateUpload(key, { status: 'error', ...describeUploadError(error) })
    } finally {
      controllers.current.delete(key)
    }
  }

  const handleFiles = (selected: File[]) => {
    const added: UploadItem[] = selected.map((file) => {
      const key = `upload-${nextKey.current++}`
      const problem = validateFile(file)
      return problem
        ? { key, file, status: 'error', loaded: 0, total: file.size, message: problem, retryable: false }
        : { key, file, status: 'uploading', loaded: 0, total: file.size }
    })
    setUploads((current) => [...added, ...current])
    added.filter((item) => item.status === 'uploading').forEach((item) => void startUpload(item.key, item.file))
  }

  const handleRetry = (key: string) => {
    const item = uploads.find((u) => u.key === key)
    if (!item) return
    updateUpload(key, { status: 'uploading', loaded: 0, total: item.file.size, message: undefined, retryable: undefined })
    void startUpload(key, item.file)
  }

  const handleDownload = async (file: UploadedFile) => {
    setActionError(null)
    setDownloadingId(file.id)
    try {
      await downloadFile(file)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        setFiles((current) => current?.filter((f) => f.id !== file.id) ?? current)
        setActionError(`Pliku „${file.originalFileName}” już nie ma – mógł zostać usunięty w innej karcie.`)
      } else {
        setActionError(`Nie udało się pobrać pliku „${file.originalFileName}”.`)
      }
    } finally {
      setDownloadingId(null)
    }
  }

  const handleDeleted = (id: string) => {
    setFiles((current) => current?.filter((f) => f.id !== id) ?? current)
    setToDelete(null)
  }

  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <main className="mx-auto w-full max-w-3xl px-4 pb-16 sm:px-8">
        <header className="pt-5 pb-1">
          <h1 className="text-lg font-semibold">Pliki</h1>
        </header>
        <p className="mb-4 text-sm text-slate-600">
          Materiały do nauki, z których powstaną fiszki. Pliki widzisz tylko Ty.
        </p>

        <FileDropZone onFiles={handleFiles} />
        <UploadQueue
          items={uploads}
          onCancel={(key) => controllers.current.get(key)?.abort()}
          onRetry={handleRetry}
          onDismiss={removeUpload}
        />
        <p role="status" className="sr-only">
          {announcement}
        </p>

        <h2 className="mt-8 mb-2 text-sm font-semibold text-slate-700">Twoje pliki</h2>
        {actionError && (
          <div className="mb-3">
            <Alert tone="error">{actionError}</Alert>
          </div>
        )}
        {listError ? (
          <div className="text-sm text-slate-600">
            <p>{listError}</p>
            <button type="button" onClick={() => void loadFiles()} className="mt-2 font-medium text-blue-700 hover:underline">
              Spróbuj ponownie
            </button>
          </div>
        ) : files === null ? (
          <p role="status" className="text-sm text-slate-500">
            Wczytywanie…
          </p>
        ) : files.length === 0 ? (
          <p className="text-sm text-slate-600">Nie masz jeszcze plików. Dodaj pierwszy powyżej.</p>
        ) : (
          <ul aria-label="Lista plików" className="divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200">
            {files.map((file) => (
              <li key={file.id} className="flex items-center gap-3 py-2 pr-1 pl-3 hover:bg-slate-50 sm:pr-3">
                <span className="w-11 shrink-0 rounded bg-slate-100 py-0.5 text-center text-[11px] font-semibold text-slate-600">
                  {fileKindLabel(file.originalFileName)}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium" title={file.originalFileName}>
                    {file.originalFileName}
                  </p>
                  <p className="text-xs text-slate-500">
                    {formatFileSize(file.sizeBytes)} · dodano {formatUpdatedAt(file.createdAt)}
                  </p>
                </div>
                <div className="flex shrink-0 sm:gap-0.5">
                  <IconButton
                    label={`Pobierz „${file.originalFileName}”`}
                    title="Pobierz"
                    touch
                    disabled={downloadingId === file.id}
                    onClick={() => void handleDownload(file)}
                  >
                    <DownloadIcon />
                  </IconButton>
                  <IconButton
                    label={`Usuń „${file.originalFileName}”`}
                    title="Usuń"
                    tone="danger"
                    touch
                    onClick={() => setToDelete(file)}
                  >
                    <TrashIcon />
                  </IconButton>
                </div>
              </li>
            ))}
          </ul>
        )}
      </main>

      {toDelete && <DeleteFileDialog file={toDelete} onClose={() => setToDelete(null)} onDeleted={handleDeleted} />}
    </div>
  )
}
