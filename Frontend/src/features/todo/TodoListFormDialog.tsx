import { useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { TODO_LIST_TITLE_MAX_LENGTH, createTodoList, isTodoListConflict, renameTodoList } from './api'
import type { TodoList } from './types'

interface TodoListFormDialogProps {
  /** Brak = tworzenie nowej listy. */
  list?: TodoList
  onClose: () => void
  onSaved: (list: TodoList) => void
  /** Serwer zgłosił, że listę zmieniono lub usunięto w innym miejscu – panel list powinien to odzwierciedlić. */
  onChangedElsewhere: (current: TodoList | { deletedId: string }) => void
}

export function TodoListFormDialog({ list, onClose, onSaved, onChangedElsewhere }: TodoListFormDialogProps) {
  const [title, setTitle] = useState(list?.title ?? '')
  const [version, setVersion] = useState(list?.version ?? 0)
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  // Tę samą regułę sprawdza backend (TodoListService); tu tylko po to, żeby nie czekać na serwer.
  const trimmed = title.trim()
  const titleError = trimmed ? undefined : 'Podaj nazwę listy.'

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (titleError) return

    setSaving(true)
    setSaveError(null)
    try {
      const saved = list ? await renameTodoList(list.id, trimmed, version) : await createTodoList(trimmed)
      onSaved(saved)
    } catch (error) {
      if (isTodoListConflict(error)) {
        // Nazwa jest jedynym polem, więc wystarczy pokazać, co jest na serwerze; kolejne „Zapisz” ją nadpisze.
        setVersion(error.current.version)
        setSaveError(`W międzyczasie nazwę zmieniono na „${error.current.title}”. Zapisz ponownie, aby ją nadpisać.`)
        onChangedElsewhere(error.current)
      } else if (list && error instanceof ApiError && error.status === 404) {
        setSaveError('Ta lista została w międzyczasie usunięta.')
        onChangedElsewhere({ deletedId: list.id })
      } else if (error instanceof ApiError && error.status === 400) {
        setSaveError(error.message)
      } else {
        setSaveError('Nie udało się zapisać listy. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  return (
    <Dialog
      title={list ? 'Zmień nazwę listy' : 'Nowa lista'}
      onClose={onClose}
      onSubmit={(event) => void handleSubmit(event)}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : list ? 'Zapisz' : 'Utwórz'}
          </Button>
        </>
      }
    >
      <TextField
        label="Nazwa"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        maxLength={TODO_LIST_TITLE_MAX_LENGTH}
        placeholder="np. Na egzamin z baz danych"
        autoComplete="off"
        error={showErrors ? titleError : undefined}
        data-autofocus
      />
      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
