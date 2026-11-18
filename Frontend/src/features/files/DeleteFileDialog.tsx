import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteFile } from './api'
import type { UploadedFile } from './types'

interface DeleteFileDialogProps {
  file: UploadedFile
  onClose: () => void
  onDeleted: (id: string) => void
}

export function DeleteFileDialog({ file, onClose, onDeleted }: DeleteFileDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteFile(file.id)
      onDeleted(file.id)
    } catch (err) {
      // Już usunięty (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(file.id)
        return
      }
      setError('Nie udało się usunąć pliku.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć plik „${file.originalFileName}”?`}
      description="Plik zniknie z serwera. Tej operacji nie można cofnąć."
      onClose={onClose}
      busy={deleting}
      footer={
        <>
          {/* Fokus na „Anuluj”, żeby Enter nie usuwał przypadkiem. */}
          <Button variant="secondary" onClick={onClose} disabled={deleting} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleDelete()} disabled={deleting} className="min-h-11 sm:min-h-0">
            {deleting ? 'Usuwanie…' : 'Usuń plik'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
