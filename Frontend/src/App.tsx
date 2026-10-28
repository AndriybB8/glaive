import { useEffect, useState } from 'react'
import { NavTabs } from './components/ui/NavTabs'
import { NotesPage } from './features/notes/NotesPage'
import { SchedulesPage } from './features/schedules/SchedulesPage'
import {RegisterPage} from "./features/auth/RegisterPage.tsx";
import {LoginPage} from './features/auth/LoginPage'
import { getToken, clearToken } from './lib/authToken'
import { Button } from './components/ui/Button'


const pages = [
  { id: 'notatki', label: 'Notatki' },
  { id: 'harmonogramy', label: 'Harmonogramy' },
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

  return (
    <div className="flex h-dvh flex-col bg-white text-slate-900">
      <NavTabs
        brand="StudyPlanner"
        tabs={pages}
        current={page}
        actions={
          <Button
            variant="ghost-danger"
            onClick={() => {
              clearToken()
              setIsAuthenticated(false)
            }}>Wyloguj się</Button>
        }
      />
      <div className="min-h-0 flex-1">{page === 'harmonogramy' ? <SchedulesPage /> : <NotesPage />}</div>
    </div>
  )
}

export default App
