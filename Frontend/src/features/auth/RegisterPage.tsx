import {useState, type FormEvent} from "react";
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { register } from './api'

const MIN_PASSWORD_LENGTH = 8;

export function RegisterPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const trimmedEmail = email.trim()
  const emailError = !trimmedEmail
    ? 'Podaj adres e-mail.'
    : !trimmedEmail.includes('@')
      ? 'Adres e-mail jest nieprawidłowy.'
      : undefined
  const passwordError =
    password.length < MIN_PASSWORD_LENGTH ? `Hasło musi mieć co najmniej ${MIN_PASSWORD_LENGTH} znaków.` : undefined

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (emailError || passwordError) return

    setSaving(true)
    setSaveError(null)
    try {
      await register({email: trimmedEmail, password})
      setSuccess(true)
    } catch (error) {
      setSaveError(error instanceof ApiError && error.status === 400 ? error.message : 'Nie udało się zarejestrować. Spróbuj ponownie.')
    } finally {
      setSaving(false)
    }
    }

    if (success) {
      return (
        <div className="mx-auto max-w-sm px-4 py-10">
          <p className="text-sm text-slate-700">Konto zostało utworzone. Możesz się teraz zalogować.</p>
        </div>
      )
    }

    return (
      <form onSubmit={(event) => void handleSubmit(event)} className="mx-auto flex max-w-sm flex-col gap-4 px-4 py-10">
        <h1 className="text-lg font-semibold text-slate-900">Rejestracja</h1>

        <TextField
          label="E-mail"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          autoComplete="email"
          error={showErrors ? emailError : undefined}
        />

        <TextField
          label="Hasło"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          autoComplete="new-password"
          error={showErrors ? passwordError : undefined}
        />

        {saveError && <Alert tone="error">{saveError}</Alert>}

        <Button type="submit" disabled={saving}>
          {saving ? 'Rejestrowanie…' : 'Zarejestruj się'}
        </Button>
      </form>
    )
}
