export function formatNumber(number: number, decimals = 1): string  {
  return number.toFixed(decimals).toString().replace(".", ",");
}
