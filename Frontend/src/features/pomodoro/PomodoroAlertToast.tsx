import { usePomodoro, type PomodoroAlert } from './pomodoroContext'

function alertMessage(alert: PomodoroAlert): string {
  if (alert.endedPhase !== 'work') return 'Koniec przerwy – czas wracać do pracy.'
  return alert.nextPhase === 'longBreak' ? 'Koniec pracy – czas na długą przerwę.' : 'Koniec pracy – czas na krótką przerwę.'
}

/**
 * Komunikat o końcu fazy Pomodoro na dole ekranu, na każdej zakładce aplikacji.
 * To sygnał wizualny, który działa zawsze: przy wyciszonym dźwięku i wtedy, gdy przeglądarka dźwięk zablokuje.
 */
export function PomodoroAlertToast() {
  const { alert, dismissAlert, soundSettings } = usePomodoro()

  return (
    // Region istnieje zawsze, żeby czytniki ekranu ogłosiły pojawiający się komunikat.
    <div role="status" aria-live="polite" className="pointer-events-none fixed inset-x-0 bottom-4 z-20 flex justify-center px-4">
      {alert && (
        <div className="pointer-events-auto flex w-full max-w-md items-start gap-3 rounded-xl bg-slate-900 px-4 py-3 text-sm text-white shadow-lg">
          <span className="mt-0.5 text-base" aria-hidden>
            🔔
          </span>
          <div className="min-w-0 flex-1">
            <p className="font-medium">{alertMessage(alert)}</p>
            {alert.soundBlocked ? (
              <p className="mt-0.5 text-slate-300">Przeglądarka zablokowała dźwięk. Kliknij w dowolne miejsce strony, aby go odblokować.</p>
            ) : (
              soundSettings.muted && <p className="mt-0.5 text-slate-300">Dźwięk jest wyciszony.</p>
            )}
          </div>
          <button
            type="button"
            onClick={dismissAlert}
            className="-my-1 min-h-9 shrink-0 rounded-md px-3 font-medium text-white hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-white"
          >
            OK
          </button>
        </div>
      )}
    </div>
  )
}
