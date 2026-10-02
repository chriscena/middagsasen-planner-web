// Sesongens startår for et tidspunkt (lokal tid). Sesongen starter 1. juli:
// juli–desember tilhører sesongen som starter samme år, januar–juni forrige år.
export function getSeasonStartYear(
  value: Date | string | number | null | undefined
): number | null {
  if (value === null || value === undefined || value === "") return null;
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.getMonth() >= 6 ? date.getFullYear() : date.getFullYear() - 1;
}
