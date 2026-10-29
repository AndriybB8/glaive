import { useEffect, useState } from 'react'
import { NavTabs } from './components/ui/NavTabs'
import { NotesPage } from './features/notes/NotesPage'
import { SchedulesPage } from './features/schedules/SchedulesPage'
import {RegisterPage} from "./features/auth/RegisterPage.tsx";
import {LoginPage} from './features/auth/LoginPage'
import { getToken, clearToken } from './lib/authToken'
import { Button } from './components/ui/Button'
import { PomodoroProvider } from './features/pomodoro/PomodoroProvider'
import { PomodoroPage } from './features/pomodoro/PomodoroPage'
import { PomodoroNavBadge } from './features/pomodoro/PomodoroNavBadge'
import { clearStoredPomodoro } from './features/pomodoro/pomodoroContext'


const pages = [
  { id: 'notatki', label: 'Notatki' },
  { id: 'harmonogramy', label: 'Harmonogramy' },
  { id: 'pomodoro', label: 'Pomodoro' },
] as const

type PageId = (typeof pages)[number]['id']

function pageFromHash(): PageId {
  const id = window.location.hash.slice(1)
  return pages.find((page) => page.id === id)?.id ?? 'notatki'
}

// Prosta nawigacja po adresie z „#” (#notatki, #harmonogramy) – odświeżenie strony zostawia ten sam widok.
// Gdy widoków przybędzie albo będą potrzebne podstrony, warto przejść na router.
function App() {
  const [page, setPage] = useState(pageFromHash)
  const [isAuthenticated, setIsAuthenticated] = useState(() => getToken() !== null)
  const [authScreen, setAuthScreen] = useState<'login' | 'register'>('login')

  useEffect(() => {
    const handleHashChange = () => setPage(pageFromHash())
    window.addEventListener('hashchange', handleHashChange)
    return () => window.removeEventListener('hashchange', handleHashChange)
  }, [])

  if (!isAuthenticated) {
    return authScreen === 'login' ? (
      <LoginPage onLoggedIn={() => setIsAuthenticated(true)} onSwitchToRegister={() => setAuthScreen('register')} />
    ) : (
      <RegisterPage onSwitchToLogin={() => setAuthScreen('login')} />
    )
  }

  // PomodoroProvider jest nad zakładkami, żeby timer działał dalej po przejściu do notatek czy harmonogramów.
  return (
    <PomodoroProvider>
      <div className="flex h-dvh flex-col bg-white text-slate-900">
        <NavTabs
          brand="StudyPlanner"
          tabs={pages}
          current={page}
          actions={
            <div className="flex items-center gap-2">
              {page !== 'pomodoro' && <PomodoroNavBadge />}
              <Button
                variant="ghost-danger"
                onClick={() => {
                  clearToken()
                  clearStoredPomodoro()
                  setIsAuthenticated(false)
                }}>Wyloguj się</Button>
            </div>
          }
        />
        <div className="min-h-0 flex-1">
          {page === 'harmonogramy' ? <SchedulesPage /> : page === 'pomodoro' ? <PomodoroPage /> : <NotesPage />}
        </div>
      </div>
    </PomodoroProvider>
  )
}

export default App
