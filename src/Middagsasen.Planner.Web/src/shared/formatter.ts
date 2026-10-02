// Tegnet som vises når en verdi mangler (f.eks. timer som ikke er beregnet).
export const MISSING_VALUE = "–";

export function formatNumber(
  number: number | null | undefined,
  decimals = 1
): string {
  if (number === null || number === undefined || Number.isNaN(number)) {
    return MISSING_VALUE;
  }
  return number.toFixed(decimals).replace(".", ",");
}

// Timer med enhet ("1,5 t"), eller bare tankestrek når timene mangler.
export function formatHours(hours: number | null | undefined): string {
  const formatted = formatNumber(hours);
  return formatted === MISSING_VALUE ? formatted : `${formatted} t`;
}
