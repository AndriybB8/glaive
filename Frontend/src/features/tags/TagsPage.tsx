import { useCallback, useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon } from '../../components/ui/icons'
import { getTags } from './api'
import { DeleteTagDialog } from './DeleteTagDialog'
import { TagChip } from './TagChip'
import { TagFormDialog } from './TagFormDialog'
import type { Tag } from './types'

type OpenDialog = { kind: 'create' } | { kind: 'edit'; tag: Tag } | { kind: 'delete'; tag: Tag } | null

const LOAD_ERROR = 'Nie udało się wczytać tagów. Sprawdź, czy backend jest uruchomiony.'

/** Ta sama kolejność co z backendu (TagService): alfabetycznie, bez względu na wielkość liter. */
function sortTags(tags: Tag[]): Tag[] {
  return [...tags].sort((a, b) => a.name.localeCompare(b.name, 'pl', { sensitivity: 'base' }))
}

export function TagsPage() {
  const [tags, setTags] = useState<Tag[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)

  const loadTags = useCallback(async () => {
    try {
      setListError(null)
      setTags(await getTags())
    } catch {
      setListError(LOAD_ERROR)
    }
  }, [])

  useEffect(() => {
    let ignore = false
    getTags()
      .then((result) => {
        if (!ignore) setTags(result)
      })
      .catch(() => {
        if (!ignore) setListError(LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [])

  const closeDialog = () => setDialog(null)

  const upsert = (saved: Tag) =>
    setTags((current) => sortTags([...(current ?? []).filter((t) => t.id !== saved.id), saved]))

  const remove = (id: string) => setTags((current) => current?.filter((t) => t.id !== id) ?? current)

  const handleSaved = (saved: Tag) => {
    upsert(saved)
    setDialog(null)
  }

  const handleDeleted = (id: string) => {
    remove(id)
    setDialog(null)
  }

  const handleChangedElsewhere = (current: Tag | { deletedId: string }) => {
    if ('deletedId' in current) remove(current.deletedId)
    else upsert(current)
  }

  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <main className="mx-auto w-full max-w-3xl px-4 pb-16 sm:px-8">
        {/* Nagłówek jak w zakładce Harmonogramy: tytuł text-lg font-semibold i przycisk akcji. */}
        <header className="flex items-center justify-between gap-2 pt-5 pb-1">
          <h1 className="text-lg font-semibold">Tagi</h1>
          <Button onClick={() => setDialog({ kind: 'create' })}>Nowy tag</Button>
        </header>
        <p className="mb-4 text-sm text-slate-600">Tagi pomagają oznaczać i odnajdywać zadania z różnych harmonogramów.</p>

        {listError ? (
          <div className="text-sm text-slate-600">
            <p>{listError}</p>
            <button type="button" onClick={() => void loadTags()} className="mt-2 font-medium text-blue-700 hover:underline">
              Spróbuj ponownie
            </button>
          </div>
        ) : tags === null ? (
          <p role="status" className="text-sm text-slate-500">
            Wczytywanie…
          </p>
        ) : tags.length === 0 ? (
          <p className="text-sm text-slate-600">Nie masz jeszcze tagów. Utwórz pierwszy przyciskiem powyżej.</p>
        ) : (
          <ul aria-label="Lista tagów" className="divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200">
            {tags.map((tag) => (
              <li key={tag.id} className="flex items-center gap-3 py-2 pr-1 pl-4 hover:bg-slate-50 sm:pr-3">
                <div className="min-w-0 flex-1">
                  <TagChip name={tag.name} color={tag.color} />
                </div>
                <span className="hidden font-mono text-xs text-slate-500 uppercase sm:inline">{tag.color}</span>
                <div className="flex shrink-0 sm:gap-0.5">
                  <IconButton label={`Edytuj tag „${tag.name}”`} title="Edytuj" touch onClick={() => setDialog({ kind: 'edit', tag })}>
                    <PencilIcon />
                  </IconButton>
                  <IconButton
                    label={`Usuń tag „${tag.name}”`}
                    title="Usuń"
                    tone="danger"
                    touch
                    onClick={() => setDialog({ kind: 'delete', tag })}
                  >
                    <TrashIcon />
                  </IconButton>
                </div>
              </li>
            ))}
          </ul>
        )}
      </main>

      {dialog?.kind === 'create' && (
        <TagFormDialog existingTags={tags ?? []} onClose={closeDialog} onSaved={handleSaved} onChangedElsewhere={handleChangedElsewhere} />
      )}
      {dialog?.kind === 'edit' && (
        <TagFormDialog
          tag={dialog.tag}
          existingTags={tags ?? []}
          onClose={closeDialog}
          onSaved={handleSaved}
          onChangedElsewhere={handleChangedElsewhere}
        />
      )}
      {dialog?.kind === 'delete' && <DeleteTagDialog tag={dialog.tag} onClose={closeDialog} onDeleted={handleDeleted} />}
    </div>
  )
}
