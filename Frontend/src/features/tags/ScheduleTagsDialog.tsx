import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon } from '../../components/ui/icons'
import { DeleteTagDialog } from './DeleteTagDialog'
import { TagChip } from './TagChip'
import { sortTags } from './tagColors'
import { TagFormDialog } from './TagFormDialog'
import type { Tag } from './types'

type Mode = { kind: 'list' } | { kind: 'create' } | { kind: 'edit'; tag: Tag } | { kind: 'delete'; tag: Tag }

interface ScheduleTagsDialogProps {
  scheduleId: string
  scheduleName: string
  /** Tagi harmonogramu – trzyma je widok harmonogramu (filtr, wybór przy zadaniu, etykiety na liście). */
  tags: readonly Tag[]
  onTagsChange: (tags: Tag[]) => void
  onClose: () => void
}

/**
 * Zarządzanie tagami jednego harmonogramu (SCRUM-162) – zamiast globalnej zakładki „Tagi”.
 * Formularz i potwierdzenie usunięcia zastępują listę na czas działania (bez okna w oknie), a potem do niej wracają.
 * Otwierane tylko przy uprawnieniu do edycji harmonogramu – tak samo sprawdza backend.
 */
export function ScheduleTagsDialog({ scheduleId, scheduleName, tags, onTagsChange, onClose }: ScheduleTagsDialogProps) {
  const [mode, setMode] = useState<Mode>({ kind: 'list' })
  const backToList = () => setMode({ kind: 'list' })

  const upsert = (saved: Tag) => onTagsChange(sortTags([...tags.filter((t) => t.id !== saved.id), saved]))
  const remove = (id: string) => onTagsChange(tags.filter((t) => t.id !== id))

  if (mode.kind === 'create' || mode.kind === 'edit') {
    return (
      <TagFormDialog
        scheduleId={scheduleId}
        tag={mode.kind === 'edit' ? mode.tag : undefined}
        existingTags={tags}
        onClose={backToList}
        onSaved={(saved) => {
          upsert(saved)
          backToList()
        }}
        onChangedElsewhere={(current) => ('deletedId' in current ? remove(current.deletedId) : upsert(current))}
      />
    )
  }

  if (mode.kind === 'delete') {
    return (
      <DeleteTagDialog
        scheduleId={scheduleId}
        tag={mode.tag}
        onClose={backToList}
        onDeleted={(id) => {
          remove(id)
          backToList()
        }}
      />
    )
  }

  return (
    <Dialog
      title="Tagi harmonogramu"
      description={`Tagi „${scheduleName}” – widzą je wszystkie osoby z dostępem do tego harmonogramu.`}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} className="min-h-11 sm:min-h-0">
            Zamknij
          </Button>
          <Button onClick={() => setMode({ kind: 'create' })} className="min-h-11 sm:min-h-0" data-autofocus>
            Nowy tag
          </Button>
        </>
      }
    >
      {tags.length === 0 ? (
        <p className="text-sm text-slate-600">Ten harmonogram nie ma jeszcze tagów. Utwórz pierwszy przyciskiem poniżej.</p>
      ) : (
        <ul aria-label="Lista tagów" className="max-h-80 divide-y divide-slate-200 overflow-y-auto rounded-lg border border-slate-200">
          {tags.map((tag) => (
            <li key={tag.id} className="flex items-center gap-3 py-1.5 pr-1 pl-3">
              <div className="min-w-0 flex-1">
                <TagChip name={tag.name} color={tag.color} />
              </div>
              <IconButton label={`Edytuj tag „${tag.name}”`} title="Edytuj" touch onClick={() => setMode({ kind: 'edit', tag })}>
                <PencilIcon />
              </IconButton>
              <IconButton label={`Usuń tag „${tag.name}”`} title="Usuń" tone="danger" touch onClick={() => setMode({ kind: 'delete', tag })}>
                <TrashIcon />
              </IconButton>
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  )
}
