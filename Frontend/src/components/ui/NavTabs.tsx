interface NavTab {
  id: string
  label: string
}

interface NavTabsProps {
  brand: string
  tabs: readonly NavTab[]
  current: string
}

/** Górny pasek aplikacji z przełączaniem widoków. Zakładki prowadzą do `#<id>`. */
export function NavTabs({ brand, tabs, current }: NavTabsProps) {
  return (
    <header className="flex h-12 shrink-0 items-stretch gap-4 overflow-x-auto border-b border-slate-200 bg-white px-4 sm:gap-6 sm:px-5">
      <span className="flex items-center text-sm font-semibold whitespace-nowrap text-slate-900">{brand}</span>
      <nav aria-label="Główna nawigacja" className="flex items-stretch gap-1">
        {tabs.map((tab) => {
          const isActive = tab.id === current
          return (
            <a
              key={tab.id}
              href={`#${tab.id}`}
              aria-current={isActive ? 'page' : undefined}
              className={`flex items-center px-3 text-sm whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-blue-600 ${
                isActive
                  ? 'font-medium text-slate-900 shadow-[inset_0_-2px_0] shadow-blue-600'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              {tab.label}
            </a>
          )
        })}
      </nav>
    </header>
  )
}
