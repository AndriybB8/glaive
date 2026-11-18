import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { CloseIcon } from '../../components/ui/icons'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { formatFileSize } from './fileRules'

/**
 * Plik w trakcie wysyłania albo taki, którego nie udało się dodać. Udane wysłanie znika stąd i trafia na listę plików.
 * - `uploading` – trwa wysyłanie (pasek postępu, „Anuluj”),
 * - `error` – odrzucony przez przeglądarkę lub serwer albo przerwany (komunikat, „Ponów” gdy ma sens),
 * - `duplicate` – serwer ma już plik o tej samej treści, więc nie dodał kopii.
 */
export interface UploadItem {
  key: string
  file: File
  status: 'uploading' | 'error' | 'duplicate'
  loaded: number
  total: number
  message?: string
  /** Czy „Ponów” ma sens (błąd sieci lub serwera, przerwanie) – nie przy pliku odrzuconym przez zasady. */
  retryable?: boolean
}

interface UploadQueueProps {
  items: UploadItem[]
  onCancel: (key: string) => void
  onRetry: (key: string) => void
  onDismiss: (key: string) => void
}

export function UploadQueue({ items, onCancel, onRetry, onDismiss }: UploadQueueProps) {
  if (items.length === 0) return null

  return (
    <ul aria-label="Wysyłane pliki" className="mt-3 space-y-2">
      {items.map((item) => {
        const percent = item.total > 0 ? Math.round((item.loaded / item.total) * 100) : 0
        // Wszystko wysłane, czekamy na odpowiedź serwera (zapis na dysk, skrót SHA-256) – bez tego pasek „stoi” na 100%.
        const saving = item.status === 'uploading' && item.total > 0 && item.loaded >= item.total
        return (
          <li
            key={item.key}
            className={`rounded-lg border px-3 py-2 text-sm ${
              item.status === 'error' ? 'border-red-200 bg-red-50' : item.status === 'duplicate' ? 'border-slate-200 bg-slate-50' : 'border-slate-200'
            }`}
          >
            <div className="flex items-center gap-2">
              <span className="min-w-0 flex-1 truncate font-medium" title={item.file.name}>
                {item.file.name}
              </span>
              {item.status === 'uploading' ? (
                <>
                  <span className="shrink-0 text-xs text-slate-500 tabular-nums">
                    {saving ? 'Zapisywanie…' : `${formatFileSize(item.loaded)} / ${formatFileSize(item.total)}`}
                  </span>
                  {!saving && (
                    <Button variant="ghost" onClick={() => onCancel(item.key)} className="min-h-11 shrink-0 sm:min-h-0">
                      Anuluj
                    </Button>
                  )}
                </>
              ) : (
                <>
                  {item.status === 'error' && item.retryable && (
                    <Button variant="ghost" onClick={() => onRetry(item.key)} className="min-h-11 shrink-0 sm:min-h-0">
                      Ponów
                    </Button>
                  )}
                  <IconButton label={`Ukryj „${item.file.name}”`} title="Ukryj" touch onClick={() => onDismiss(item.key)}>
                    <CloseIcon />
                  </IconButton>
                </>
              )}
            </div>
            {item.status === 'uploading' ? (
              <div className="mt-1.5 flex items-center gap-2">
                <div className="flex-1">
                  <ProgressBar value={item.loaded} max={item.total} label={`Wysyłanie „${item.file.name}”`} />
                </div>
                <span className="w-10 shrink-0 text-right text-xs text-slate-500 tabular-nums">{percent}%</span>
              </div>
            ) : (
              <p className={`mt-0.5 ${item.status === 'error' ? 'text-red-800' : 'text-slate-600'}`}>{item.message}</p>
            )}
          </li>
        )
      })}
    </ul>
  )
}
