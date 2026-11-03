import { useCallback, useEffect, useState } from 'react'
import { ChecklistIcon } from '../../components/ui/icons'
import { getTodoLists } from './api'
import { DeleteTodoListDialog } from './DeleteTodoListDialog'
import { TodoListFormDialog } from './TodoListFormDialog'
import { TodoListView } from './TodoListView'
import type { TodoList, TodoListSummary } from './types'

const LOAD_ERROR = 'Nie udało się wczytać list. Sprawdź, czy backend jest uruchomiony.'

type OpenDialog = { kind: 'create' } | { kind: 'rename'; list: TodoListSummary } | { kind: 'delete'; list: TodoListSummary } | null

/** Ta sama kolejność co z backendu (TodoListService): alfabetycznie, przy równych nazwach – starsze pierwsze. */
function sortLists(lists: TodoListSummary[]): TodoListSummary[] {
  return [...lists].sort(
    (a, b) => a.title.localeCompare(b.title, 'pl', { sensitivity: 'base' }) || a.createdAt.localeCompare(b.createdAt),
  )
}

/** Zakładka „Listy”: listy rzeczy do zrobienia po lewej, punkty wybranej listy po prawej (na telefonie – jedno albo drugie). */
export function TodoListsPage() {
  const [lists, setLists] = useState<TodoListSummary[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)
  const selected = lists?.find((l) => l.id === selectedId) ?? null

  const loadLists = useCallback(async () => {
    try {
      setListError(null)
      setLists(await getTodoLists())
    } catch {
      setListError(LOAD_ERROR)
    }
  }, [])

  useEffect(() => {
    let ignore = false
    getTodoLists()
      .then((result) => {
        if (!ignore) setLists(result)
      })
      .catch(() => {
        if (!ignore) setListError(LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [])

  const handleCountsChange = useCallback((listId: string, itemCount: number, doneCount: number) => {
    setLists((current) => {
      if (!current) return current
      const target = current.find((l) => l.id === listId)
      if (!target || (target.itemCount === itemCount && target.doneCount === doneCount)) return current
      return current.map((l) => (l.id === listId ? { ...l, itemCount, doneCount } : l))
    })
  }, [])

  /** Utworzona albo przemianowana lista – liczniki zostają, bo zmiana nazwy ich nie dotyczy. */
  const upsertList = (saved: TodoList) => {
    setLists((current) => {
      const existing = current?.find((l) => l.id === saved.id)
      const summary: TodoListSummary = { ...saved, itemCount: existing?.itemCount ?? 0, doneCount: existing?.doneCount ?? 0 }
      return sortLists([...(current ?? []).filter((l) => l.id !== saved.id), summary])
    })
  }

  const removeList = (id: string) => {
    setLists((current) => current?.filter((l) => l.id !== id) ?? current)
    setSelectedId((current) => (current === id ? null : current))
  }

  const handleSaved = (saved: TodoList) => {
    const isNew = !lists?.some((l) => l.id === saved.id)
    upsertList(saved)
    // Nowa lista od razu się otwiera – można dodawać punkty.
    if (isNew) setSelectedId(saved.id)
    setDialog(null)
  }

  const handleChangedElsewhere = (current: TodoList | { deletedId: string }) => {
    if ('deletedId' in current) removeList(current.deletedId)
    else upsertList(current)
  }

  return (
    <div className="flex h-full bg-white text-slate-900">
      <aside
        className={`w-full flex-col border-r border-slate-200 bg-slate-50 md:flex md:w-72 md:shrink-0 ${selected ? 'hidden' : 'flex'}`}
      >
        <div className="flex items-center justify-between gap-2 px-4 pt-5 pb-3">
          <h1 className="text-lg font-semibold">Listy</h1>
          <button
            type="button"
            onClick={() => setDialog({ kind: 'create' })}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          >
            Nowa lista
          </button>
        </div>

        <nav aria-label="Listy rzeczy do zrobienia" className="min-h-0 flex-1 overflow-y-auto px-2 pb-4">
          {listError ? (
            <div className="px-2 py-4 text-sm text-slate-600">
              <p>{listError}</p>
              <button type="button" onClick={() => void loadLists()} className="mt-2 font-medium text-blue-700 hover:underline">
                Spróbuj ponownie
              </button>
            </div>
          ) : lists === null ? (
            <p className="px-2 py-4 text-sm text-slate-500">Wczytywanie…</p>
          ) : lists.length === 0 ? (
            <p className="px-2 py-4 text-sm text-slate-600">
              Nie masz jeszcze list. Utwórz pierwszą, np. „Na egzamin z baz danych”, przyciskiem powyżej.
            </p>
          ) : (
            <ul className="space-y-0.5">
              {lists.map((l) => {
                const isActive = l.id === selectedId
                const allDone = l.itemCount > 0 && l.doneCount === l.itemCount
                return (
                  <li key={l.id}>
                    <button
                      type="button"
                      onClick={() => setSelectedId(l.id)}
                      aria-current={isActive ? 'page' : undefined}
                      className={`flex min-h-11 w-full items-center gap-2.5 rounded-md px-3 py-2 text-left text-sm focus-visible:outline-2 focus-visible:outline-blue-600 ${
                        isActive ? 'bg-white font-medium text-slate-900 shadow-[inset_2px_0_0] shadow-blue-600' : 'text-slate-700 hover:bg-slate-100'
                      }`}
                    >
                      <span className={`shrink-0 ${isActive ? 'text-blue-600' : 'text-slate-400'}`}>
                        <ChecklistIcon />
                      </span>
                      <span className="min-w-0 flex-1 truncate">{l.title}</span>
                      <span className={`shrink-0 text-xs tabular-nums ${allDone ? 'font-medium text-emerald-700' : 'text-slate-500'}`}>
                        {l.doneCount}/{l.itemCount}
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </nav>
      </aside>

      <main className={`min-w-0 flex-1 overflow-y-auto ${selected ? 'flex' : 'hidden md:flex'}`}>
        {selected ? (
          <TodoListView
            key={selected.id}
            list={selected}
            onCountsChange={handleCountsChange}
            onRename={() => setDialog({ kind: 'rename', list: selected })}
            onDelete={() => setDialog({ kind: 'delete', list: selected })}
            onBack={() => setSelectedId(null)}
          />
        ) : (
          <div className="m-auto max-w-xs px-6 text-center text-sm text-slate-500">Wybierz listę albo utwórz nową.</div>
        )}
      </main>

      {(dialog?.kind === 'create' || dialog?.kind === 'rename') && (
        <TodoListFormDialog
          list={dialog.kind === 'rename' ? dialog.list : undefined}
          onClose={() => setDialog(null)}
          onSaved={handleSaved}
          onChangedElsewhere={handleChangedElsewhere}
        />
      )}
      {dialog?.kind === 'delete' && (
        <DeleteTodoListDialog
          list={dialog.list}
          onClose={() => setDialog(null)}
          onDeleted={(id) => {
            removeList(id)
            setDialog(null)
          }}
        />
      )}
    </div>
  )
}
