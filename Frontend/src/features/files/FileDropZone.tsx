import { useRef, useState, type DragEvent } from 'react'
import { Button } from '../../components/ui/Button'
import { UploadIcon } from '../../components/ui/icons'
import { ACCEPT_ATTRIBUTE, ACCEPTED_LABEL, formatFileSize, MAX_FILE_SIZE_BYTES } from './fileRules'

interface FileDropZoneProps {
  onFiles: (files: File[]) => void
}

/** Pole na pliki: przeciągnięcie i upuszczenie albo przycisk „Wybierz pliki” (działa też z klawiatury i na telefonie). */
export function FileDropZone({ onFiles }: FileDropZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (!event.dataTransfer.types.includes('Files')) return
    event.preventDefault()
    event.dataTransfer.dropEffect = 'copy'
    setDragging(true)
  }

  const handleDragLeave = (event: DragEvent<HTMLDivElement>) => {
    // Opuszczenie elementu wewnątrz pola też wywołuje dragleave – reagujemy tylko na wyjście poza pole.
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragging(false)
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    const files = Array.from(event.dataTransfer.files)
    if (files.length > 0) onFiles(files)
  }

  return (
    <div
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      data-testid="file-drop-zone"
      className={`flex flex-col items-center gap-2 rounded-lg border-2 border-dashed px-4 py-8 text-center transition-colors ${
        dragging ? 'border-blue-500 bg-blue-50' : 'border-slate-300 bg-slate-50'
      }`}
    >
      <span className={dragging ? 'text-blue-600' : 'text-slate-400'}>
        <UploadIcon />
      </span>
      <p className="text-sm text-slate-700">
        <span className="hidden sm:inline">Przeciągnij pliki tutaj albo </span>
        <Button variant="secondary" onClick={() => inputRef.current?.click()} className="min-h-11 sm:ml-1 sm:min-h-0">
          Wybierz pliki
        </Button>
      </p>
      <p className="text-xs text-slate-500">
        {ACCEPTED_LABEL}, maksymalnie {formatFileSize(MAX_FILE_SIZE_BYTES)} na plik.
      </p>
      <input
        ref={inputRef}
        type="file"
        multiple
        accept={ACCEPT_ATTRIBUTE}
        aria-label="Wybierz pliki do wysłania"
        className="sr-only"
        tabIndex={-1}
        onChange={(event) => {
          const files = Array.from(event.target.files ?? [])
          // Czyszczenie wartości – ten sam plik można wybrać ponownie (np. po błędzie).
          event.target.value = ''
          if (files.length > 0) onFiles(files)
        }}
      />
    </div>
  )
}
