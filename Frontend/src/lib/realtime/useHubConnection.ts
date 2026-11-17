import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { getToken } from '../authToken'

/** Stan połączenia na żywo – do wskaźnika „Na żywo” (LiveStatusBadge). */
export type LiveStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline'

/** Odstępy kolejnych prób, gdy połączenie się zerwie (automatycznie) albo nie uda się go nawiązać (ręcznie). */
const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 30000]
const RETRY_DELAYS_MS = [2000, 5000, 10000, 30000]

interface HubOptions {
  /** Funkcje dla zdarzeń z serwera: nazwa zdarzenia = nazwa metody interfejsu klienta huba w backendzie. */
  events: Record<string, (message: never) => void>
  /**
   * Po ponownym połączeniu: wiadomości z czasu przerwy przepadły, więc trzeba pobrać aktualny stan.
   * Backend nic nie przywraca – odbiorców wyznacza przy każdej zmianie, więc wystarczy się połączyć.
   */
  onResync: () => void
}

/**
 * Połączenie z hubem SignalR na czas, gdy komponent jest zamontowany (huby list i harmonogramów).
 * Token idzie w adresie (access_token) – przeglądarka nie pozwala dodać nagłówka do WebSocketa; backend czyta go tylko na ścieżkach hubów.
 * Funkcje z `options` mogą się zmieniać przy każdym renderze – połączenie i tak korzysta z najnowszych.
 */
export function useHubConnection(url: string, options: HubOptions): LiveStatus {
  const [status, setStatus] = useState<LiveStatus>('connecting')
  const optionsRef = useRef(options)

  useEffect(() => {
    optionsRef.current = options
  })

  useEffect(() => {
    let disposed = false
    let retryTimer: number | undefined
    let failedStarts = 0

    const connection = new HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory: () => getToken() ?? '' })
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      // Problemy z połączeniem pokazuje wskaźnik „Na żywo”; bez tego biblioteka zgłaszałaby w konsoli m.in. przerwany start przy podwójnym montażu w StrictMode.
      .configureLogging(LogLevel.Critical)
      .build()

    // Lista zdarzeń jest stała dla danego huba, więc rejestrujemy je raz, przy tworzeniu połączenia.
    for (const name of Object.keys(optionsRef.current.events)) {
      connection.on(name, (message: never) => optionsRef.current.events[name]?.(message))
    }

    connection.onreconnecting(() => {
      if (!disposed) setStatus('reconnecting')
    })
    connection.onreconnected(() => {
      if (disposed) return
      setStatus('connected')
      optionsRef.current.onResync()
    })
    // Automatyczne wznawianie się poddało albo serwer zamknął połączenie (np. wygasł token) – próbujemy dalej sami.
    connection.onclose(() => {
      if (disposed) return
      setStatus('offline')
      scheduleStart()
    })

    function scheduleStart() {
      const delay = RETRY_DELAYS_MS[Math.min(failedStarts, RETRY_DELAYS_MS.length - 1)]
      failedStarts++
      retryTimer = window.setTimeout(() => void start(), delay)
    }

    async function start() {
      if (disposed) return
      try {
        await connection.start()
        if (disposed) return
        const recovered = failedStarts > 0
        failedStarts = 0
        setStatus('connected')
        if (recovered) optionsRef.current.onResync()
      } catch {
        // Przerwane przez odmontowanie (np. podwójny montaż w StrictMode) albo serwer niedostępny.
        if (disposed) return
        setStatus('offline')
        scheduleStart()
      }
    }

    void start()

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      void connection.stop()
    }
  }, [url])

  return status
}
