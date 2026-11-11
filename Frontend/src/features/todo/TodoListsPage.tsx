import { useCallback, useEffect, useRef, useState } from 'react'
import { ChecklistIcon, UsersIcon } from '../../components/ui/icons'
import { getCurrentUser } from '../../lib/authToken'
import { getTodoLists } from './api'
import { DeleteTodoListDialog } from './DeleteTodoListDialog'
import { describeChange, describeInvitation } from './describeChange'
import { LeaveTodoListDialog } from './LeaveTodoListDialog'
import { LiveStatusBadge } from './LiveStatusBadge'
import { LiveToasts, type LiveToast } from './LiveToasts'
import { useTodoListHub, type TodoListChangedMessage, type TodoListInvitationReceivedMessage } from './realtime'
import { ShareTodoListDialog } from './ShareTodoListDialog'
import { ACCESS_LABELS, getTodoListInvitations } from './sharingApi'
import { TodoInvitations } from './TodoInvitations'
import { TodoListFormDialog } from './TodoListFormDialog'
import { TodoListView } from './TodoListView'
import type { TodoList, TodoListInvitation, TodoListSummary } from './types'

const LOAD_ERROR = 'Nie udało się wczytać list. Sprawdź, czy backend jest uruchomiony.'

/** Zmiany na żywo: jak długo widać powiadomienie i podświetlony punkt, ile powiadomień naraz. */
const TOAST_MS = 6000
const HIGHLIGHT_MS = 5000
const MAX_TOASTS = 3
/** Zmiany przychodzą seriami – panel list pobieramy raz po chwili ciszy. */
const LISTS_REFRESH_DELAY_MS = 300

type OpenDialog =
  | { kind: 'create' }
  | { kind: 'rename'; list: TodoListSummary }
  | { kind: 'delete'; list: TodoListSummary }
  | { kind: 'share'; list: TodoListSummary }
  | { kind: 'leave'; list: TodoListSummary }
  | null

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
  const [invitations, setInvitations] = useState<TodoListInvitation[]>([])
  const selected = lists?.find((l) => l.id === selectedId) ?? null

  // --- Zmiany na żywo (SignalR) ---
  const [me] = useState(getCurrentUser)
  const [toasts, setToasts] = useState<LiveToast[]>([])
  /** Sygnał dla otwartej listy: pobierz punkty od nowa. */
  const [itemsRefreshSignal, setItemsRefreshSignal] = useState(0)
  /** Punkty właśnie zmienione przez inne osoby: id → { email, numer zmiany } (numer, żeby stary timer nie zgasił nowszej zmiany). */
  const [changedItems, setChangedItems] = useState<Record<string, { email: string; seq: number }>>({})
  const liveSeqRef = useRef(0)
  const listsRefreshTimerRef = useRef<number | undefined>(undefined)

  const loadLists = useCallback(async () => {
    try {
      setListError(null)
      setLists(await getTodoLists())
    } catch {
      setListError(LOAD_ERROR)
    }
  }, [])

  /** Panel list po zmianie na żywo: po cichu (błąd zostawia poprzedni stan) i raz na serię zmian. */
  const refreshListsQuietly = useCallback(() => {
    window.clearTimeout(listsRefreshTimerRef.current)
    listsRefreshTimerRef.current = window.setTimeout(() => {
      getTodoLists()
        .then(setLists)
        .catch(() => undefined)
    }, LISTS_REFRESH_DELAY_MS)
  }, [])

  useEffect(() => () => window.clearTimeout(listsRefreshTimerRef.current), [])

  const dismissToast = useCallback((id: number) => setToasts((current) => current.filter((t) => t.id !== id)), [])

  const pushToast = (actor: string, text: string) => {
    const id = ++liveSeqRef.current
    setToasts((current) => [...current, { id, actor: actor || 'Ktoś', text }].slice(-MAX_TOASTS))
    window.setTimeout(() => dismissToast(id), TOAST_MS)
  }

  const highlightItem = (itemId: string, email: string) => {
    const seq = ++liveSeqRef.current
    setChangedItems((current) => ({ ...current, [itemId]: { email, seq } }))
    window.setTimeout(() => {
      setChangedItems((current) => {
        if (current[itemId]?.seq !== seq) return current
        const next = { ...current }
        delete next[itemId]
        return next
      })
    }, HIGHLIGHT_MS)
  }

  const handleListChanged = (message: TodoListChangedMessage) => {
    const isOpen = message.listId === selectedId
    const fromSomeoneElse = message.actorUserId !== me?.id

    // Stan zawsze z API – także przy własnych zmianach, bo mogły przyjść z innej karty.
    refreshListsQuietly()
    if (isOpen) setItemsRefreshSignal((n) => n + 1)

    if (fromSomeoneElse && isOpen && message.itemId && (message.change === 'itemCreated' || message.change === 'itemUpdated')) {
      highlightItem(message.itemId, message.actorEmail || 'Ktoś')
    }

    const listTitle = lists?.find((l) => l.id === message.listId)?.title ?? null
    const text = describeChange(message, me, listTitle, isOpen)
    if (text) pushToast(message.actorEmail, text)
  }

  const handleInvitationReceived = (message: TodoListInvitationReceivedMessage) => {
    void loadInvitations()
    if (message.actorUserId !== me?.id) pushToast(message.actorEmail, describeInvitation(message))
  }

  const liveStatus = useTodoListHub({
    onListChanged: handleListChanged,
    onInvitationReceived: handleInvitationReceived,
    // Po przerwie w połączeniu wiadomości przepadły – pobieramy wszystko od nowa.
    onResync: () => {
      refreshListsQuietly()
      void loadInvitations()
      setItemsRefreshSignal((n) => n + 1)
    },
  })

  /** Zaproszenia to dodatek do panelu – błąd wczytania po cichu zostawia poprzedni stan. */
  const loadInvitations = useCallback(async () => {
    try {
      setInvitations(await getTodoListInvitations())
    } catch {
      // Spróbujemy ponownie przy następnym powrocie do karty.
    }
  }, [])

  // Zaproszenia przychodzą od innych osób, więc sprawdzamy je też po powrocie do karty.
  useEffect(() => {
    let ignore = false
    getTodoListInvitations()
      .then((result) => {
        if (!ignore) setInvitations(result)
      })
      .catch(() => undefined)
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') void loadInvitations()
    }
    document.addEventListener('visibilitychange', handleVisibility)
    return () => {
      ignore = true
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [loadInvitations])

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

  /** Przyjęte zaproszenie: lista jest już nasza (z dostępem) – wczytujemy listy i od razu ją otwieramy. */
  const handleInvitationAccepted = async (invitation: TodoListInvitation) => {
    setInvitations((current) => current.filter((i) => i.listId !== invitation.listId))
    await loadLists()
    setSelectedId(invitation.listId)
  }

  /** Uprawnienia do listy zmieniły się w innym miejscu (403) albo lista zniknęła – wczytujemy listy od nowa. */
  const handleAccessChanged = useCallback(() => {
    void loadLists()
  }, [loadLists])

  const handleShareListGone = useCallback(() => {
    setDialog(null)
    void loadLists()
  }, [loadLists])

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
          <div className="flex min-w-0 flex-col gap-0.5">
            <h1 className="text-lg font-semibold">Listy</h1>
            <LiveStatusBadge status={liveStatus} />
          </div>
          <button
            type="button"
            onClick={() => setDialog({ kind: 'create' })}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          >
            Nowa lista
          </button>
        </div>

        <TodoInvitations
          invitations={invitations}
          onAccepted={(invitation) => void handleInvitationAccepted(invitation)}
          onRemoved={(listId) => setInvitations((current) => current.filter((i) => i.listId !== listId))}
        />

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
                      {l.access !== 'owner' && (
                        <span className="shrink-0 text-slate-400" title={`Udostępniona · ${ACCESS_LABELS[l.access]}`}>
                          <UsersIcon />
                          <span className="sr-only">Udostępniona, {ACCESS_LABELS[l.access]}</span>
                        </span>
                      )}
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
            onShare={() => setDialog({ kind: 'share', list: selected })}
            onLeave={() => setDialog({ kind: 'leave', list: selected })}
            onAccessChanged={handleAccessChanged}
            onBack={() => setSelectedId(null)}
            refreshSignal={itemsRefreshSignal}
            changedItems={Object.fromEntries(Object.entries(changedItems).map(([id, change]) => [id, change.email]))}
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
      {dialog?.kind === 'share' && (
        <ShareTodoListDialog list={dialog.list} onClose={() => setDialog(null)} onListGone={handleShareListGone} />
      )}
      {dialog?.kind === 'leave' && (
        <LeaveTodoListDialog
          list={dialog.list}
          onClose={() => setDialog(null)}
          onLeft={(id) => {
            removeList(id)
            setDialog(null)
          }}
        />
      )}
      <LiveToasts toasts={toasts} onDismiss={dismissToast} />

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
