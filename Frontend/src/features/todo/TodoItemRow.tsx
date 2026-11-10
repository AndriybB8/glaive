import { useState, type DragEvent, type KeyboardEvent } from 'react'
import { IconButton } from '../../components/ui/IconButton'
import { ChevronDownIcon, ChevronUpIcon, GripIcon, TrashIcon } from '../../components/ui/icons'
import { TODO_ITEM_TITLE_MAX_LENGTH } from './api'
import type { TodoItem } from './types'

interface TodoItemRowProps {
  item: TodoItem
  index: number
  count: number
  /** Ten punkt jest właśnie przeciągany. */
  dragging: boolean
  /** Tylko podgląd (lista udostępniona z uprawnieniem „Podgląd”) – bez odhaczania, edycji, przestawiania i usuwania. */
  readOnly?: boolean
  onToggle: (item: TodoItem) => void
  /** Zwraca false, gdy zapis się nie udał – wtedy pole edycji zostaje otwarte. */
  onRename: (item: TodoItem, title: string) => Promise<boolean>
  onDelete: (item: TodoItem) => void
  onMove: (from: number, to: number) => void
  onDragStart: (item: TodoItem) => void
  onDragOver: (item: TodoItem) => void
  onDragEnd: () => void
}

/**
 * Punkt listy: odhaczanie (pole wyboru), edycja treści (kliknięcie w treść), usuwanie i zmiana miejsca
 * (przeciąganie na komputerze albo przyciski wyżej/niżej – działają też z klawiatury i na telefonie).
 */
export function TodoItemRow({
  item,
  index,
  count,
  dragging,
  readOnly = false,
  onToggle,
  onRename,
  onDelete,
  onMove,
  onDragStart,
  onDragOver,
  onDragEnd,
}: TodoItemRowProps) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(item.title)
  const [saving, setSaving] = useState(false)

  const startEditing = () => {
    setDraft(item.title)
    setEditing(true)
  }

  const finishEditing = async () => {
    if (saving) return
    const title = draft.trim()
    // Pusta albo niezmieniona treść – wracamy do podglądu bez zapisu.
    if (!title || title === item.title) {
      setEditing(false)
      return
    }
    setSaving(true)
    const ok = await onRename(item, title)
    setSaving(false)
    if (ok) setEditing(false)
  }

  const handleEditKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault()
      void finishEditing()
    } else if (event.key === 'Escape') {
      event.preventDefault()
      setEditing(false)
    }
  }

  const handleDragStart = (event: DragEvent<HTMLLIElement>) => {
    event.dataTransfer.effectAllowed = 'move'
    // Firefox nie zacznie przeciągania bez danych.
    event.dataTransfer.setData('text/plain', item.id)
    onDragStart(item)
  }

  const handleDragOver = (event: DragEvent<HTMLLIElement>) => {
    event.preventDefault()
    event.dataTransfer.dropEffect = 'move'
    onDragOver(item)
  }

  return (
    <li
      draggable={!editing && !readOnly}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDrop={(event) => event.preventDefault()}
      onDragEnd={onDragEnd}
      className={`group flex items-center gap-1 rounded-lg border bg-white py-1 pr-1 pl-1 sm:gap-2 sm:pl-2 ${
        dragging ? 'border-blue-300 opacity-50' : 'border-slate-200'
      }`}
    >
      {!readOnly && (
        <span className="hidden shrink-0 cursor-grab text-slate-400 active:cursor-grabbing sm:block" title="Przeciągnij, aby zmienić kolejność" aria-hidden>
          <GripIcon />
        </span>
      )}

      <span className="flex size-11 shrink-0 items-center justify-center sm:size-8">
        <input
          type="checkbox"
          checked={item.isDone}
          onChange={() => onToggle(item)}
          disabled={readOnly}
          aria-label={`Zrobione: ${item.title}`}
          className="size-5 cursor-pointer accent-blue-600 disabled:cursor-default"
        />
      </span>

      {editing ? (
        <input
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={handleEditKeyDown}
          onBlur={() => void finishEditing()}
          maxLength={TODO_ITEM_TITLE_MAX_LENGTH}
          aria-label="Treść punktu"
          disabled={saving}
          autoFocus
          className="min-h-9 min-w-0 flex-1 rounded-md border border-blue-600 px-2 text-sm outline-2 -outline-offset-1 outline-blue-600"
        />
      ) : readOnly ? (
        <span className={`min-w-0 flex-1 px-1 py-2 text-sm break-words ${item.isDone ? 'text-slate-400 line-through' : 'text-slate-900'}`}>
          {item.title}
        </span>
      ) : (
        <button
          type="button"
          onClick={startEditing}
          title="Kliknij, aby edytować"
          aria-label={`Edytuj: ${item.title}`}
          className={`min-h-9 min-w-0 flex-1 cursor-text rounded-md px-1 text-left text-sm break-words hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-blue-600 ${
            item.isDone ? 'text-slate-400 line-through' : 'text-slate-900'
          }`}
        >
          {item.title}
        </button>
      )}

      {!readOnly && (
        <div className="flex shrink-0 items-center">
          <IconButton label={`Przesuń wyżej: ${item.title}`} title="Przesuń wyżej" touch disabled={index === 0} onClick={() => onMove(index, index - 1)}>
            <ChevronUpIcon />
          </IconButton>
          <IconButton
            label={`Przesuń niżej: ${item.title}`}
            title="Przesuń niżej"
            touch
            disabled={index === count - 1}
            onClick={() => onMove(index, index + 1)}
          >
            <ChevronDownIcon />
          </IconButton>
          <IconButton label={`Usuń: ${item.title}`} title="Usuń" tone="danger" touch onClick={() => onDelete(item)}>
            <TrashIcon />
          </IconButton>
        </div>
      )}
    </li>
  )
}
