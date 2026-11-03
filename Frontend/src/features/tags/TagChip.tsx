import { readableTextColor } from './tagColors'

interface TagChipProps {
  name: string
  color: string
}

/** Tag w swoim kolorze. Do użycia wszędzie, gdzie tag jest wyświetlany (np. później przy zadaniach). */
export function TagChip({ name, color }: TagChipProps) {
  return (
    <span
      className="inline-flex max-w-full items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
      style={{ backgroundColor: color, color: readableTextColor(color) }}
    >
      <span className="truncate">{name}</span>
    </span>
  )
}
