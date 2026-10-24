import { NotesPage } from './features/notes/NotesPage'

// Tymczasowo aplikacja pokazuje od razu notatki. Gdy pojawi się routing/nawigacja,
// NotesPage trafi pod własną ścieżkę (np. /notes).
function App() {
  return <NotesPage />
}

export default App
