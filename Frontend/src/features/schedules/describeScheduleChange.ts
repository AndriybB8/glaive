import type { ScheduleChangedMessage } from './realtime'

/**
 * Opis zmiany, np. „dodaje zadanie „Kolokwium””. Czas teraźniejszy – nie zależy od płci.
 * Wspólny dla powiadomień i historii zmian.
 *
 * @param meId id zalogowanego użytkownika – do „Twoje uprawnienia”, „odbiera Ci dostęp”
 * @param scheduleName nazwa harmonogramu, jeśli trzeba ją dopisać (zmiana poza otwartym harmonogramem)
 */
export function describeScheduleChange(message: ScheduleChangedMessage, meId: string | null, scheduleName: string | null = null): string {
  const task = message.taskTitle ? ` „${message.taskTitle}”` : ''
  const inSchedule = scheduleName ? ` w harmonogramie „${scheduleName}”` : ''
  const schedule = `„${message.scheduleName ?? scheduleName ?? 'harmonogram'}”`
  const aboutMe = meId !== null && message.memberUserId === meId

  switch (message.change) {
    case 'taskCreated':
      return `dodaje zadanie${task}${inSchedule}`
    case 'taskUpdated':
      return `zmienia zadanie${task}${inSchedule}`
    case 'taskDeleted':
      return `usuwa zadanie${task}${inSchedule}`
    case 'taskTagsChanged':
      return `zmienia tagi zadania${task}${inSchedule}`
    case 'scheduleUpdated':
      return `zmienia nazwę lub daty harmonogramu ${schedule}`
    case 'scheduleDeleted':
      return `usuwa harmonogram ${schedule}`
    case 'memberJoined':
      return `dołącza do harmonogramu ${schedule}`
    case 'memberRoleChanged':
      return aboutMe ? `zmienia Twoje uprawnienia do harmonogramu ${schedule}` : `zmienia uprawnienia do harmonogramu ${schedule}`
    case 'memberRemoved':
      if (message.memberUserId === message.actorUserId) return `opuszcza harmonogram ${schedule}`
      return aboutMe ? `odbiera Ci dostęp do harmonogramu ${schedule}` : `odbiera dostęp do harmonogramu ${schedule}`
    default:
      return 'zmienia harmonogram'
  }
}
