import { TodayTomorrowWidget } from './TodayTomorrowWidget'

/**
 * Zakładka „Pulpit” (epik Dashboard): strona startowa z najważniejszymi rzeczami na teraz.
 * Na razie widżet „Dziś i jutro” (SCRUM-79); kolejne widżety (np. powiadomienia, SCRUM-88) dochodzą pod nim.
 */
export function DashboardPage() {
  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <main className="mx-auto w-full max-w-5xl px-4 pb-16 sm:px-8">
        <header className="pt-5 pb-4">
          <h1 className="text-lg font-semibold">Pulpit</h1>
        </header>
        <TodayTomorrowWidget />
      </main>
    </div>
  )
}
