import { useEffect, useId, useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { IconButton } from '../../components/ui/IconButton'
import { CloseIcon, TrashIcon } from '../../components/ui/icons'
import { SelectField } from '../../components/ui/SelectField'
import { TextField } from '../../components/ui/TextField'
import { getCurrentUser } from '../../lib/authToken'
import { ApiError } from '../../lib/http'
import { MEMBER_ROLE_OPTIONS, isMemberRole, looksLikeEmail, lookupUser } from './sharing'
import type { Member, MemberRole, MembersApi, UserLookup } from './types'

const TOO_MANY_REQUESTS = 'Za dużo prób w krótkim czasie. Odczekaj minutę i spróbuj ponownie.'

/** Teksty zależne od tego, co udostępniamy (rodzaj gramatyczny, odmiana). */
export interface ShareDialogTexts {
  /** Tytuł okna, np. „Udostępnij „Sesja zimowa””. */
  title: string
  description: string
  /** Co może dana rola – pod listą wyboru uprawnień. */
  roleDescriptions: Record<MemberRole, string>
  /** Gdy nikt jeszcze nie ma dostępu, np. „Harmonogram nie jest jeszcze nikomu udostępniony.” */
  empty: string
  /** Po zaproszeniu, np. „Harmonogram pojawi się u tej osoby po przyjęciu zaproszenia.” */
  afterInvite: string
  /** Znaleziona osoba ma już dostęp, np. „Ta osoba ma już dostęp do harmonogramu.” */
  alreadyMember: string
  /** Znaleziony własny adres, np. „…masz już pełny dostęp do harmonogramu.” */
  ownAddress: string
}

interface ShareDialogProps {
  texts: ShareDialogTexts
  api: MembersApi
  onClose: () => void
  /** Udostępniana rzecz zniknęła albo nie jest już nasza (404/403) – widok pod oknem powinien się odświeżyć. */
  onGone: () => void
}

/**
 * Okno udostępniania (tylko właściciel): wyszukanie osoby po adresie email, wybór uprawnień (podgląd / edycja)
 * i zaproszenie. Niżej osoby z dostępem i oczekujące zaproszenia – ze zmianą uprawnień i odbieraniem dostępu.
 * Wspólne dla różnych zasobów – adresy API i teksty przychodzą w propsach.
 */
export function ShareDialog({ texts, api, onClose, onGone }: ShareDialogProps) {
  const me = getCurrentUser()
  const roleDescriptionId = useId()
  const membersHeadingId = useId()

  const [members, setMembers] = useState<Member[] | null>(null)
  const [membersError, setMembersError] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [email, setEmail] = useState('')
  const [showEmailError, setShowEmailError] = useState(false)
  const [searching, setSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  /** Znalezione konto – dopiero wtedy można wybrać uprawnienia i zaprosić. */
  const [found, setFound] = useState<UserLookup | null>(null)
  const [role, setRole] = useState<MemberRole>('viewer')
  const [inviting, setInviting] = useState(false)
  const [inviteMessage, setInviteMessage] = useState<string | null>(null)

  /** Wiersz, przy którym trwa zapis (zmiana uprawnień albo odbieranie dostępu). */
  const [busyUserId, setBusyUserId] = useState<string | null>(null)
  const [memberError, setMemberError] = useState<string | null>(null)

  const trimmedEmail = email.trim()
  const emailError = !trimmedEmail ? 'Podaj adres email.' : looksLikeEmail(trimmedEmail) ? undefined : 'To nie wygląda na adres email.'
  const foundIsMe = found !== null && me !== null && found.id === me.id
  const foundMember = found ? members?.find((m) => m.userId === found.id) : undefined

  useEffect(() => {
    let ignore = false
    api
      .getMembers()
      .then((result) => {
        if (!ignore) {
          setMembers(result)
          setMembersError(null)
        }
      })
      .catch((error: unknown) => {
        if (ignore) return
        if (error instanceof ApiError && (error.status === 404 || error.status === 403)) {
          onGone()
          return
        }
        setMembersError('Nie udało się wczytać osób z dostępem.')
      })
    return () => {
      ignore = true
    }
  }, [api, onGone, reloadKey])

  const handleEmailChange = (value: string) => {
    setEmail(value)
    // Inny adres = nowe wyszukiwanie; wynik poprzedniego już nie pasuje.
    setFound(null)
    setSearchError(null)
  }

  const handleSearch = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowEmailError(true)
    setInviteMessage(null)
    if (emailError || searching) return

    setSearching(true)
    setSearchError(null)
    setFound(null)
    try {
      setFound(await lookupUser(trimmedEmail))
      setRole('viewer')
    } catch (error) {
      if (error instanceof ApiError && (error.status === 404 || error.status === 400)) setSearchError(error.message)
      else if (error instanceof ApiError && error.status === 429) setSearchError(TOO_MANY_REQUESTS)
      else setSearchError('Nie udało się wyszukać użytkownika. Spróbuj ponownie.')
    } finally {
      setSearching(false)
    }
  }

  const handleInvite = async () => {
    if (!found || inviting) return
    setInviting(true)
    setSearchError(null)
    try {
      const member = await api.invite(found.email, role)
      setMembers((current) => [...(current ?? []).filter((m) => m.userId !== member.userId), member])
      setInviteMessage(`Wysłano zaproszenie do ${member.email}. ${texts.afterInvite}`)
      setFound(null)
      setEmail('')
      setShowEmailError(false)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setSearchError(error.message)
        setReloadKey((key) => key + 1)
      } else if (error instanceof ApiError && error.status === 429) {
        setSearchError(TOO_MANY_REQUESTS)
      } else if (error instanceof ApiError && error.status === 400) {
        setSearchError(error.message)
      } else if (error instanceof ApiError && error.status === 403) {
        onGone()
      } else if (error instanceof ApiError && error.status === 404) {
        // Konto albo udostępniana rzecz zniknęły w międzyczasie. Ponowne wczytanie osób wykryje usunięcie.
        setSearchError(error.message)
        setFound(null)
        setReloadKey((key) => key + 1)
      } else {
        setSearchError('Nie udało się wysłać zaproszenia. Spróbuj ponownie.')
      }
    } finally {
      setInviting(false)
    }
  }

  const handleRoleChange = async (member: Member, next: MemberRole) => {
    if (next === member.role) return
    setBusyUserId(member.userId)
    setMemberError(null)
    setInviteMessage(null)
    // Od razu na ekranie; przy błędzie wracamy do poprzedniej wartości.
    setMembers((current) => current?.map((m) => (m.userId === member.userId ? { ...m, role: next } : m)) ?? current)
    try {
      const saved = await api.changeRole(member.userId, next)
      setMembers((current) => current?.map((m) => (m.userId === saved.userId ? saved : m)) ?? current)
    } catch (error) {
      setMembers((current) => current?.map((m) => (m.userId === member.userId ? { ...m, role: member.role } : m)) ?? current)
      handleMemberError(error, member, 'Nie udało się zmienić uprawnień.')
    } finally {
      setBusyUserId(null)
    }
  }

  const handleRemove = async (member: Member) => {
    setBusyUserId(member.userId)
    setMemberError(null)
    setInviteMessage(null)
    try {
      await api.remove(member.userId)
      setMembers((current) => current?.filter((m) => m.userId !== member.userId) ?? current)
    } catch (error) {
      handleMemberError(error, member, member.acceptedAt ? 'Nie udało się odebrać dostępu.' : 'Nie udało się cofnąć zaproszenia.')
    } finally {
      setBusyUserId(null)
    }
  }

  const handleMemberError = (error: unknown, member: Member, fallback: string) => {
    if (error instanceof ApiError && error.status === 404) {
      // Osoba w międzyczasie zrezygnowała lub odrzuciła zaproszenie (albo wszystko zniknęło – wykryje to ponowne wczytanie).
      setMembers((current) => current?.filter((m) => m.userId !== member.userId) ?? current)
      setReloadKey((key) => key + 1)
    } else if (error instanceof ApiError && error.status === 403) {
      onGone()
    } else if (error instanceof ApiError && error.status === 400) {
      setMemberError(error.message)
    } else {
      setMemberError(fallback)
    }
  }

  return (
    <Dialog
      title={texts.title}
      description={texts.description}
      onClose={onClose}
      footer={
        <Button onClick={onClose} className="col-span-2 min-h-11 sm:min-h-0">
          Gotowe
        </Button>
      }
    >
      <section aria-label="Zaproś osobę" className="flex flex-col gap-3">
        <form noValidate onSubmit={(event) => void handleSearch(event)} className="flex items-start gap-2">
          <TextField
            label="Adres email"
            type="email"
            value={email}
            onChange={(event) => handleEmailChange(event.target.value)}
            placeholder="np. jan.kowalski@student.pcz.pl"
            autoComplete="off"
            error={showEmailError ? emailError : undefined}
            className="flex-1"
            data-autofocus
          />
          {/* Odstęp = wysokość etykiety pola, żeby przycisk stał w jednej linii z polem. */}
          <Button type="submit" variant="secondary" disabled={searching} className="mt-[26px] min-h-11 shrink-0 sm:min-h-10">
            {searching ? 'Szukanie…' : 'Szukaj'}
          </Button>
        </form>

        {searchError && <Alert tone="error">{searchError}</Alert>}
        {inviteMessage && (
          <p role="status" className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
            {inviteMessage}
          </p>
        )}

        {found && (
          <div className="flex flex-col gap-3 rounded-lg border border-slate-200 p-3">
            <div className="flex items-center gap-2">
              <p className="min-w-0 flex-1 truncate text-sm">
                <span className="text-slate-500">Znaleziono: </span>
                <span className="font-medium text-slate-900">{found.email}</span>
              </p>
              <IconButton label="Wyczyść wynik wyszukiwania" title="Wyczyść" onClick={() => handleEmailChange('')}>
                <CloseIcon />
              </IconButton>
            </div>

            {foundIsMe ? (
              <p className="text-sm text-slate-600">{texts.ownAddress}</p>
            ) : foundMember ? (
              <p className="text-sm text-slate-600">
                {foundMember.acceptedAt
                  ? `${texts.alreadyMember} Uprawnienia zmienisz niżej.`
                  : 'Ta osoba ma już zaproszenie, na które jeszcze nie odpowiedziała.'}
              </p>
            ) : (
              <>
                <SelectField
                  label="Uprawnienia"
                  options={MEMBER_ROLE_OPTIONS}
                  value={role}
                  onChange={(event) => {
                    if (isMemberRole(event.target.value)) setRole(event.target.value)
                  }}
                  aria-describedby={roleDescriptionId}
                />
                <p id={roleDescriptionId} className="-mt-1.5 text-sm text-slate-500">
                  {texts.roleDescriptions[role]}
                </p>
                <Button onClick={() => void handleInvite()} disabled={inviting} className="min-h-11 sm:min-h-9">
                  {inviting ? 'Wysyłanie…' : 'Zaproś'}
                </Button>
              </>
            )}
          </div>
        )}
      </section>

      <section aria-labelledby={membersHeadingId} className="flex flex-col gap-2 border-t border-slate-200 pt-4">
        <h3 id={membersHeadingId} className="text-sm font-semibold text-slate-900">
          Osoby z dostępem
        </h3>

        {memberError && <Alert tone="error">{memberError}</Alert>}

        <ul className="flex flex-col divide-y divide-slate-100">
          <li className="flex min-h-11 items-center gap-2 py-1.5">
            <span className="min-w-0 flex-1 truncate text-sm text-slate-900">
              {me?.email ?? 'Ty'} {me && <span className="text-slate-500">(Ty)</span>}
            </span>
            <Badge tone="neutral">Właściciel</Badge>
          </li>

          {membersError ? (
            <li className="py-2 text-sm text-slate-600">
              {membersError}{' '}
              <button type="button" onClick={() => setReloadKey((key) => key + 1)} className="font-medium text-blue-700 hover:underline">
                Spróbuj ponownie
              </button>
            </li>
          ) : members === null ? (
            <li className="py-2 text-sm text-slate-500">Wczytywanie…</li>
          ) : members.length === 0 ? (
            <li className="py-2 text-sm text-slate-500">{texts.empty}</li>
          ) : (
            members.map((member) => {
              const busy = busyUserId === member.userId
              const pending = member.acceptedAt === null
              return (
                <li key={member.userId} className="flex min-h-11 items-center gap-2 py-1.5">
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm text-slate-900">{member.email}</p>
                    {pending && <p className="text-xs text-slate-500">Zaproszenie czeka na odpowiedź</p>}
                  </div>
                  <select
                    value={member.role}
                    onChange={(event) => {
                      if (isMemberRole(event.target.value)) void handleRoleChange(member, event.target.value)
                    }}
                    disabled={busy}
                    aria-label={`Uprawnienia: ${member.email}`}
                    className="h-11 shrink-0 rounded-md border border-slate-300 bg-white px-2 text-base text-slate-900 focus:border-blue-600 focus:outline-2 focus:-outline-offset-1 focus:outline-blue-600 disabled:opacity-60 sm:h-8 sm:text-sm"
                  >
                    {MEMBER_ROLE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                  <IconButton
                    label={pending ? `Cofnij zaproszenie: ${member.email}` : `Odbierz dostęp: ${member.email}`}
                    title={pending ? 'Cofnij zaproszenie' : 'Odbierz dostęp'}
                    tone="danger"
                    touch
                    disabled={busy}
                    onClick={() => void handleRemove(member)}
                  >
                    <TrashIcon />
                  </IconButton>
                </li>
              )
            })
          )}
        </ul>
      </section>
    </Dialog>
  )
}
