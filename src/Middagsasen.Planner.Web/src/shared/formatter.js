export function formatNumber(number, decimals = 1)  {
  return number.toFixed(decimals).toString().replace(".", ",");
}
