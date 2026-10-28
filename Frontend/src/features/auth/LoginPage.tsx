import { useState, type FormEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { setToken } from '../../lib/authToken'
import { login } from './api'

interface LoginPageProps {
  onLoggedIn: () => void
  onSwitchToRegister: () => void
}

export function LoginPage({ onLoggedIn, onSwitchToRegister }: LoginPageProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setSaveError(null)
    try {
      const result = await login({ email: email.trim(), password })
      setToken(result.token)
      onLoggedIn()
    } catch (error) {
      setSaveError(
        error instanceof ApiError && error.status === 400 ? error.message : 'Nie udało się zalogować. Spróbuj ponownie później.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <form noValidate onSubmit={(event) => void handleSubmit(event)} className="mx-auto flex max-w-sm flex-col gap-4 px-4 py-10">
      <h1 className="text-lg font-semibold text-slate-900">Logowanie</h1>

      <TextField label="E-mail" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" />
      <TextField
        label="Hasło"
        type="password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        autoComplete="current-password"
      />

      {saveError && <Alert tone="error">{saveError}</Alert>}

      <Button type="submit" disabled={saving}>
        {saving ? 'Logowanie…' : 'Zaloguj się'}
      </Button>

      <p className="text-center text-sm text-slate-600">
        Nie masz konta?{' '}
        <button type="button" onClick={onSwitchToRegister} className="font-medium text-blue-700 hover:underline">
          Zarejestruj się
        </button>
      </p>
    </form>
  )
}
