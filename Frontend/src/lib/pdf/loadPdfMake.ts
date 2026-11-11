/**
 * pdfmake (MIT) ładowany dopiero przy pierwszym eksporcie: biblioteka z czcionką Roboto to ok. 1 MB,
 * więc nie dokładamy jej do startu aplikacji. Kolejne eksporty korzystają z już wczytanej.
 * Roboto (Apache 2.0) ma polskie znaki i jest domyślną czcionką przeglądarkowej wersji pdfmake.
 */
let loading: Promise<typeof import('pdfmake/build/pdfmake')> | null = null

export function loadPdfMake() {
  loading ??= Promise.all([import('pdfmake/build/pdfmake'), import('pdfmake/build/vfs_fonts')])
    .then(([pdfMakeModule, vfsModule]) => {
      // Moduły CommonJS: w zależności od bundlera eksport jest w `default` albo bezpośrednio.
      const pdfMake = 'default' in pdfMakeModule ? (pdfMakeModule.default as typeof pdfMakeModule) : pdfMakeModule
      const vfs = 'default' in vfsModule ? vfsModule.default : vfsModule
      pdfMake.addVirtualFileSystem(vfs as Parameters<typeof pdfMake.addVirtualFileSystem>[0])
      return pdfMake
    })
    .catch((error: unknown) => {
      loading = null // Następna próba (np. po odzyskaniu sieci) zacznie od nowa.
      throw error
    })
  return loading
}
