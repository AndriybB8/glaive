import { useCallback, useEffect, useRef, useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { DownloadIcon, PencilIcon, TrashIcon, UserPlusIcon } from '../../components/ui/icons'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { ApiError } from '../../lib/http'
import { downloadPdf, toPdfFileName } from '../../lib/pdf'
import {
  TODO_ITEM_TITLE_MAX_LENGTH,
  createTodoItem,
  deleteTodoItem,
  getTodoItems,
  isTodoItemConflict,
  reorderTodoItems,
  setTodoItemDone,
  updateTodoItem,
} from './api'
import { ACCESS_LABELS } from './sharingApi'
import { TodoItemRow } from './TodoItemRow'
import { createTodoListPdf } from './todoListPdf'
import type { TodoItem, TodoListSummary } from './types'

const LOAD_ERROR = 'Nie udało się wczytać punktów listy.'

/** Przenosi element tablicy z miejsca `from` na `to`. */
function moveInArray<T>(array: T[], from: number, to: number): T[] {
  const result = [...array]
  const [moved] = result.splice(from, 1)
  result.splice(to, 0, moved)
  return result
}

/** Podmienia w liście punkty o tych samych id (np. nowe wersje z serwera), nie ruszając kolejności. */
function mergeItems(current: TodoItem[], updates: TodoItem[]): TodoItem[] {
  const byId = new Map(updates.map((u) => [u.id, u]))
  return current.map((item) => byId.get(item.id) ?? item)
}

interface TodoListViewProps {
  list: TodoListSummary
  /** Liczniki listy po każdej zmianie punktów – panel list pokazuje „3 z 7”. */
  onCountsChange: (listId: string, itemCount: number, doneCount: number) => void
  onRename: () => void
  onDelete: () => void
  /** Okno udostępniania – tylko właściciel. */
  onShare: () => void
  /** Opuszczenie cudzej (udostępnionej) listy. */
  onLeave: () => void
  /** Serwer odmówił (403) – uprawnienia do listy zmieniły się w międzyczasie; panel list powinien je wczytać od nowa. */
  onAccessChanged: () => void
  onBack: () => void
  /** Zwiększany, gdy ktoś (albo my w innej karcie) zmienił punkty tej listy – pobieramy je wtedy od nowa. */
  refreshSignal?: number
  /** Punkty właśnie zmienione przez inne osoby: id punktu → adres email. */
  changedItems?: Readonly<Record<string, string>>
}

/** Zmiany na żywo przychodzą seriami (np. przestawianie) – czekamy chwilę i pobieramy punkty raz. */
const LIVE_REFRESH_DELAY_MS = 250

/**
 * Punkty jednej listy: dodawanie, odhaczanie, edycja, usuwanie i zmiana kolejności.
 * Na udostępnionej liście z uprawnieniem „Podgląd” punkty są tylko do odczytu.
 */
export function TodoListView({
  list,
  onCountsChange,
  onRename,
  onDelete,
  onShare,
  onLeave,
  onAccessChanged,
  onBack,
  refreshSignal = 0,
  changedItems = {},
}: TodoListViewProps) {
  const listId = list.id
  const isOwner = list.access === 'owner'
  const readOnly = list.access === 'viewer'
  const [items, setItems] = useState<TodoItem[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [newTitle, setNewTitle] = useState('')
  const [adding, setAdding] = useState(false)
  const [draggedId, setDraggedId] = useState<string | null>(null)
  const addInputRef = useRef<HTMLInputElement>(null)
  /** Aktualne punkty poza renderowaniem (przeciąganie, kolejka zapisów kolejności). */
  const itemsRef = useRef<TodoItem[] | null>(null)
  /** Kolejność przed przeciąganiem – żeby wiedzieć, czy cokolwiek się zmieniło. */
  const dragStartOrderRef = useRef<string>('')
  /** Zapisy kolejności idą po kolei, żeby szybkie przesunięcia nie wyprzedziły się na serwerze. */
  const orderQueueRef = useRef<Promise<void>>(Promise.resolve())
  // Odświeżanie na żywo nie może podmienić punktów w trakcie przeciągania ani przed zapisem kolejności –
  // wtedy tylko je zapamiętujemy i wykonujemy, gdy użytkownik skończy.
  const draggingRef = useRef(false)
  const pendingOrderSavesRef = useRef(0)
  const refreshDeferredRef = useRef(false)
  /** Wartość sygnału przy otwarciu listy – na nią nie reagujemy (punkty i tak właśnie się wczytują). */
  const initialRefreshSignalRef = useRef(refreshSignal)

  /**
   * Jedyna droga zmiany punktów: najpierw ref (od razu widoczny dla kolejnych akcji, np. szybkich przesunięć),
   * potem stan Reacta do wyświetlenia.
   */
  const applyItems = useCallback((next: TodoItem[] | null | ((current: TodoItem[] | null) => TodoItem[] | null)) => {
    const value = typeof next === 'function' ? next(itemsRef.current) : next
    itemsRef.current = value
    setItems(value)
  }, [])

  const loadItems = useCallback(async () => {
    try {
      setLoadError(null)
      applyItems(await getTodoItems(listId))
    } catch {
      setLoadError(LOAD_ERROR)
    }
  }, [listId, applyItems])

  useEffect(() => {
    let ignore = false
    getTodoItems(listId)
      .then((result) => {
        if (!ignore) applyItems(result)
      })
      .catch(() => {
        if (!ignore) setLoadError(LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [listId, applyItems])

  /** Aktualne punkty z serwera po zmianie na żywo. Błędy po cichu: 403/404 znaczy, że zmienił się dostęp albo lista zniknęła. */
  const refreshFromServer = useCallback(async () => {
    if (draggingRef.current || pendingOrderSavesRef.current > 0) {
      refreshDeferredRef.current = true
      return
    }
    refreshDeferredRef.current = false
    try {
      const fresh = await getTodoItems(listId)
      // Użytkownik zaczął przeciągać w czasie pobierania – odświeżymy po zakończeniu.
      if (draggingRef.current || pendingOrderSavesRef.current > 0) refreshDeferredRef.current = true
      else applyItems(fresh)
    } catch (error) {
      if (error instanceof ApiError && (error.status === 403 || error.status === 404)) onAccessChanged()
    }
  }, [listId, applyItems, onAccessChanged])

  const runDeferredRefresh = useCallback(() => {
    if (refreshDeferredRef.current && !draggingRef.current && pendingOrderSavesRef.current === 0) void refreshFromServer()
  }, [refreshFromServer])

  useEffect(() => {
    if (refreshSignal === initialRefreshSignalRef.current) return
    const timer = window.setTimeout(() => void refreshFromServer(), LIVE_REFRESH_DELAY_MS)
    return () => window.clearTimeout(timer)
  }, [refreshSignal, refreshFromServer])

  // Liczniki w panelu list zawsze zgodne z tym, co widać tutaj.
  useEffect(() => {
    if (items) onCountsChange(listId, items.length, items.filter((i) => i.isDone).length)
  }, [items, listId, onCountsChange])

  /** Reakcja na błąd zapisu punktu: punkt usunięty gdzie indziej albo inny problem – wtedy wczytujemy listę od nowa. */
  const handleItemError = (error: unknown, fallback: string, item: TodoItem) => {
    if (error instanceof ApiError && error.status === 404) {
      applyItems((current) => current?.filter((i) => i.id !== item.id) ?? current)
      setActionError('Ten punkt został w międzyczasie usunięty.')
    } else if (error instanceof ApiError && error.status === 400) {
      setActionError(error.message)
    } else if (error instanceof ApiError && error.status === 403) {
      handleForbidden(error)
    } else {
      setActionError(fallback)
      void loadItems()
    }
  }

  /** Właściciel zmienił nam uprawnienia na „Podgląd” – pokazujemy komunikat serwera i aktualny stan listy. */
  const handleForbidden = (error: ApiError) => {
    setActionError(error.message)
    onAccessChanged()
    void loadItems()
  }

  const handleAdd = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    const title = newTitle.trim()
    if (!title || adding) return

    setAdding(true)
    setActionError(null)
    try {
      const created = await createTodoItem(listId, title)
      applyItems((current) => [...(current ?? []), created])
      setNewTitle('')
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) setActionError(error.message)
      else if (error instanceof ApiError && error.status === 403) handleForbidden(error)
      else if (error instanceof ApiError && error.status === 404) setActionError('Ta lista została w międzyczasie usunięta.')
      else setActionError('Nie udało się dodać punktu.')
    } finally {
      setAdding(false)
      addInputRef.current?.focus()
    }
  }

  const handleToggle = async (item: TodoItem) => {
    const isDone = !item.isDone
    setActionError(null)
    // Od razu na ekranie; przy błędzie wracamy do poprzedniego stanu.
    applyItems((current) => current?.map((i) => (i.id === item.id ? { ...i, isDone } : i)) ?? current)
    try {
      const saved = await setTodoItemDone(item.id, isDone)
      applyItems((current) => (current ? mergeItems(current, [saved]) : current))
    } catch (error) {
      applyItems((current) => current?.map((i) => (i.id === item.id ? { ...i, isDone: item.isDone } : i)) ?? current)
      handleItemError(error, 'Nie udało się zapisać zmiany.', item)
    }
  }

  const handleRename = async (item: TodoItem, title: string): Promise<boolean> => {
    setActionError(null)
    try {
      const saved = await updateTodoItem(item, title)
      applyItems((current) => (current ? mergeItems(current, [saved]) : current))
      return true
    } catch (error) {
      if (isTodoItemConflict(error)) {
        applyItems((current) => (current ? mergeItems(current, [error.current]) : current))
        setActionError('Ten punkt zmieniono w międzyczasie w innym miejscu – pokazuję aktualną treść.')
        return true
      }
      handleItemError(error, 'Nie udało się zapisać punktu.', item)
      return !(error instanceof ApiError && error.status === 400)
    }
  }

  const handleDelete = async (item: TodoItem) => {
    setActionError(null)
    applyItems((current) => current?.filter((i) => i.id !== item.id) ?? current)
    try {
      await deleteTodoItem(item.id)
    } catch (error) {
      // Już usunięty (np. w drugiej karcie) – efekt jest ten sam.
      if (error instanceof ApiError && error.status === 404) return
      setActionError('Nie udało się usunąć punktu.')
      void loadItems()
    }
  }

  /** Zapisuje kolejność z ekranu. Odpowiedź serwera niesie nowe wersje punktów – podmieniamy je, nie ruszając kolejności. */
  const saveOrder = useCallback(() => {
    pendingOrderSavesRef.current++
    orderQueueRef.current = orderQueueRef.current.then(async () => {
      const ids = itemsRef.current?.map((i) => i.id)
      if (!ids) {
        pendingOrderSavesRef.current--
        return
      }
      try {
        const saved = await reorderTodoItems(listId, ids)
        applyItems((current) => (current ? mergeItems(current, saved) : current))
      } catch (error) {
        if (error instanceof ApiError && error.status === 403) onAccessChanged()
        setActionError(
          error instanceof ApiError && error.status === 403
            ? error.message
            : error instanceof ApiError && error.status === 409
            ? 'Lista zmieniła się w międzyczasie w innym miejscu – wczytałem aktualną kolejność.'
            : 'Nie udało się zapisać kolejności.',
        )
        await loadItems()
      } finally {
        pendingOrderSavesRef.current--
        runDeferredRefresh()
      }
    })
  }, [listId, applyItems, loadItems, onAccessChanged, runDeferredRefresh])

  const handleMove = (from: number, to: number) => {
    const current = itemsRef.current
    if (!current || to < 0 || to >= current.length || from === to) return
    setActionError(null)
    applyItems(moveInArray(current, from, to))
    saveOrder()
  }

  const handleDragStart = (item: TodoItem) => {
    dragStartOrderRef.current = itemsRef.current?.map((i) => i.id).join(',') ?? ''
    draggingRef.current = true
    setDraggedId(item.id)
  }

  // Podgląd na żywo: przeciągany punkt wskakuje na miejsce punktu, nad którym jest kursor.
  const handleDragOver = (over: TodoItem) => {
    const current = itemsRef.current
    if (!current || !draggedId || over.id === draggedId) return
    const from = current.findIndex((i) => i.id === draggedId)
    const to = current.findIndex((i) => i.id === over.id)
    if (from < 0 || to < 0) return
    applyItems(moveInArray(current, from, to))
  }

  const handleDragEnd = () => {
    draggingRef.current = false
    setDraggedId(null)
    const order = itemsRef.current?.map((i) => i.id).join(',') ?? ''
    if (order !== dragStartOrderRef.current) {
      setActionError(null)
      saveOrder()
    } else {
      runDeferredRefresh()
    }
  }

  const [exporting, setExporting] = useState(false)

  /** PDF z punktami w kolejności z ekranu – działa przy każdych uprawnieniach, także „Podgląd”. */
  const handleExportPdf = async () => {
    const current = itemsRef.current
    if (!current) return
    setExporting(true)
    setActionError(null)
    try {
      await downloadPdf(createTodoListPdf({ list, items: current }), toPdfFileName(list.title, 'lista'))
    } catch {
      setActionError('Nie udało się wygenerować pliku PDF. Spróbuj ponownie.')
    } finally {
      setExporting(false)
    }
  }

  const doneCount = items?.filter((i) => i.isDone).length ?? list.doneCount
  const itemCount = items?.length ?? list.itemCount

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-4 px-4 pt-4 pb-16 sm:px-8">
      <header className="flex flex-col gap-3">
        <button
          type="button"
          onClick={onBack}
          className="self-start rounded-md px-2 py-1 text-sm text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-blue-600 md:hidden"
        >
          Wszystkie listy
        </button>
        <div className="flex items-start gap-2">
          <h2 className="min-w-0 flex-1 text-2xl font-bold tracking-tight break-words text-slate-900">{list.title}</h2>
          <IconButton
            label={exporting ? 'Generowanie pliku PDF…' : `Pobierz listę ${list.title} jako PDF`}
            title={exporting ? 'Generowanie…' : 'Pobierz PDF'}
            touch
            disabled={items === null || exporting}
            onClick={() => void handleExportPdf()}
          >
            <DownloadIcon />
          </IconButton>
          {isOwner ? (
            <>
              <IconButton label={`Udostępnij listę ${list.title}`} title="Udostępnij" touch onClick={onShare}>
                <UserPlusIcon />
              </IconButton>
              <IconButton label={`Zmień nazwę listy ${list.title}`} title="Zmień nazwę" touch onClick={onRename}>
                <PencilIcon />
              </IconButton>
              <IconButton label={`Usuń listę ${list.title}`} title="Usuń listę" tone="danger" touch onClick={onDelete}>
                <TrashIcon />
              </IconButton>
            </>
          ) : (
            <Button variant="ghost-danger" onClick={onLeave} className="min-h-11 shrink-0 sm:min-h-8">
              Opuść listę
            </Button>
          )}
        </div>
        {!isOwner && (
          <p className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
            <Badge tone="blue">Udostępniona · {ACCESS_LABELS[list.access]}</Badge>
            {readOnly ? 'Możesz tylko przeglądać tę listę.' : 'Możesz dodawać i zmieniać punkty.'}
          </p>
        )}
        <div className="flex flex-col gap-1.5">
          <p className="text-sm text-slate-600">
            {itemCount === 0 ? 'Brak punktów' : `Zrobione: ${doneCount} z ${itemCount}`}
          </p>
          <ProgressBar value={doneCount} max={itemCount} label={`Postęp listy ${list.title}`} />
        </div>
      </header>

      {!readOnly && (
        <form onSubmit={(event) => void handleAdd(event)} className="flex gap-2">
          <input
            ref={addInputRef}
            value={newTitle}
            onChange={(event) => setNewTitle(event.target.value)}
            maxLength={TODO_ITEM_TITLE_MAX_LENGTH}
            placeholder="Dodaj punkt, np. Powtórzyć normalizację"
            aria-label="Nowy punkt"
            autoComplete="off"
            className="h-11 min-w-0 flex-1 rounded-md border border-slate-300 bg-white px-3 text-base text-slate-900 placeholder:text-slate-500 focus:border-blue-600 focus:outline-2 focus:-outline-offset-1 focus:outline-blue-600 sm:h-10 sm:text-sm"
          />
          <Button type="submit" disabled={adding || !newTitle.trim()} className="min-h-11 sm:min-h-10">
            {adding ? 'Dodawanie…' : 'Dodaj'}
          </Button>
        </form>
      )}

      {actionError && <Alert tone="error">{actionError}</Alert>}

      {loadError ? (
        <div className="text-sm text-slate-600">
          <p>{loadError}</p>
          <button type="button" onClick={() => void loadItems()} className="mt-1 font-medium text-blue-700 hover:underline">
            Spróbuj ponownie
          </button>
        </div>
      ) : items === null ? (
        <p className="text-sm text-slate-500">Wczytywanie…</p>
      ) : items.length === 0 ? (
        <p className="text-sm text-slate-600">{readOnly ? 'Lista jest pusta.' : 'Lista jest pusta. Dodaj pierwszy punkt polem powyżej.'}</p>
      ) : (
        <>
          <ul aria-label={`Punkty listy ${list.title}`} className="flex flex-col gap-1.5">
            {items.map((item, index) => (
              <TodoItemRow
                key={item.id}
                item={item}
                index={index}
                count={items.length}
                dragging={item.id === draggedId}
                readOnly={readOnly}
                changedBy={changedItems[item.id]}
                onToggle={(i) => void handleToggle(i)}
                onRename={handleRename}
                onDelete={(i) => void handleDelete(i)}
                onMove={handleMove}
                onDragStart={handleDragStart}
                onDragOver={handleDragOver}
                onDragEnd={handleDragEnd}
              />
            ))}
          </ul>
          {!readOnly && (
            <p className="text-xs text-slate-500">
              Kliknij treść, aby ją edytować. Kolejność zmienisz strzałkami albo przeciągając punkt (na komputerze).
            </p>
          )}
        </>
      )}
    </article>
  )
}
