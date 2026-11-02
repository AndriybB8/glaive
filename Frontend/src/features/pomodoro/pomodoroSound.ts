/**
 * Dźwięki końca fazy Pomodoro, generowane w przeglądarce przez Web Audio API – bez plików audio w repozytorium
 * (nic do pobierania, żadnych licencji na nagrania).
 *
 * Przeglądarki pozwalają odtworzyć dźwięk dopiero po interakcji użytkownika (kliknięcie, klawisz), dlatego
 * `unlockAudio()` wołamy przy każdej akcji użytkownika. Timer startuje kliknięciem „Start”, więc zwykle
 * w chwili końca fazy dźwięk jest już odblokowany.
 */

export type PomodoroSoundId = 'bell' | 'gong' | 'digital'

export const POMODORO_SOUNDS: readonly { value: PomodoroSoundId; label: string }[] = [
  { value: 'bell', label: 'Dzwonek' },
  { value: 'gong', label: 'Gong' },
  { value: 'digital', label: 'Pik-pik' },
]

export interface PomodoroSoundSettings {
  soundId: PomodoroSoundId
  /** Głośność 0–100 (%). */
  volume: number
}

export const DEFAULT_SOUND_SETTINGS: PomodoroSoundSettings = {
  soundId: 'bell',
  volume: 70,
}

/** Klucz w localStorage. Ustawienia dźwięku są ustawieniem tego urządzenia, więc zostają po wylogowaniu. */
export const POMODORO_SOUND_STORAGE_KEY = 'SP.pomodoroSound'

/** Sprawdza wartość z localStorage; każde pole, które się nie zgadza, wraca do domyślnego. */
export function parseSoundSettings(value: unknown): PomodoroSoundSettings {
  const v = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  const soundId = POMODORO_SOUNDS.some((s) => s.value === v.soundId) ? (v.soundId as PomodoroSoundId) : DEFAULT_SOUND_SETTINGS.soundId
  const volume = Number.isFinite(v.volume) ? Math.min(100, Math.max(0, Math.round(v.volume as number))) : DEFAULT_SOUND_SETTINGS.volume
  return { soundId, volume }
}

/** Maksymalne wzmocnienie przy 100% – zostawia zapas, żeby dźwięk nie przesterował. */
const MASTER_GAIN = 0.6

type AudioContextConstructor = typeof AudioContext

let context: AudioContext | null = null

function getContext(): AudioContext | null {
  if (context) return context
  const Ctor: AudioContextConstructor | undefined =
    window.AudioContext ?? (window as unknown as { webkitAudioContext?: AudioContextConstructor }).webkitAudioContext
  if (!Ctor) return null
  context = new Ctor()
  return context
}

/** Odblokowuje dźwięk. Wołać przy kliknięciu lub klawiszu – bez interakcji przeglądarka tego nie pozwoli. */
export function unlockAudio(): void {
  const ctx = getContext()
  if (ctx && ctx.state === 'suspended') {
    void ctx.resume().catch(() => undefined)
  }
}

interface Tone {
  frequency: number
  type: OscillatorType
  /** Start względem początku dźwięku (s). */
  at: number
  /** Czas wybrzmiewania (s). */
  decay: number
  /** Głośność względna 0–1. */
  level: number
}

const SOUND_TONES: Record<PomodoroSoundId, Tone[]> = {
  // Dwa uderzenia dzwonka: podstawa + alikwoty, krótki atak i długie wybrzmienie.
  bell: [
    { frequency: 880, type: 'sine', at: 0, decay: 1.6, level: 0.6 },
    { frequency: 2430, type: 'sine', at: 0, decay: 0.6, level: 0.15 },
    { frequency: 1318.5, type: 'sine', at: 0.35, decay: 1.8, level: 0.55 },
    { frequency: 3640, type: 'sine', at: 0.35, decay: 0.6, level: 0.12 },
  ],
  // Niski, miękki gong.
  gong: [
    { frequency: 196, type: 'sine', at: 0, decay: 3, level: 0.8 },
    { frequency: 392, type: 'sine', at: 0, decay: 2.2, level: 0.35 },
    { frequency: 587, type: 'sine', at: 0, decay: 1.4, level: 0.2 },
  ],
  // Trzy krótkie piknięcia jak w zegarku cyfrowym (fala prostokątna jest głośna – niższy poziom).
  digital: [0, 0.22, 0.44].map((at) => ({ frequency: 1000, type: 'square' as const, at, decay: 0.12, level: 0.25 })),
}

/**
 * Odtwarza dźwięk. Zwraca false, gdy się nie da – brak Web Audio albo przeglądarka nie odblokowała dźwięku
 * (np. strona odświeżona i nikt jeszcze w nic nie kliknął). Nigdy nie rzuca wyjątku.
 */
export async function playPomodoroSound(soundId: PomodoroSoundId, volume: number): Promise<boolean> {
  try {
    const ctx = getContext()
    if (!ctx) return false
    if (ctx.state !== 'running') {
      await ctx.resume().catch(() => undefined)
      // `resume()` zmienia stan – TypeScript tego nie widzi, dlatego odczytujemy go ponownie jako ogólny typ.
      if ((ctx.state as AudioContextState) !== 'running') return false
    }

    const gainValue = (Math.min(100, Math.max(0, volume)) / 100) * MASTER_GAIN
    if (gainValue === 0) return true

    const master = ctx.createGain()
    master.gain.value = gainValue
    master.connect(ctx.destination)

    const start = ctx.currentTime + 0.02
    for (const tone of SOUND_TONES[soundId]) {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = tone.type
      osc.frequency.value = tone.frequency
      const t0 = start + tone.at
      gain.gain.setValueAtTime(0.0001, t0)
      gain.gain.exponentialRampToValueAtTime(tone.level, t0 + 0.01)
      gain.gain.exponentialRampToValueAtTime(0.0001, t0 + tone.decay)
      osc.connect(gain).connect(master)
      osc.start(t0)
      osc.stop(t0 + tone.decay + 0.05)
    }
    return true
  } catch {
    return false
  }
}
