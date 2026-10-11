// Norsk alfabetisk sammenligning (æ, ø, å til slutt) for sortering i UI-et.
// Én delt Collator, så alle lister sorteres likt.
const collator = new Intl.Collator("nb");

export function compareNb(a: string, b: string): number {
  return collator.compare(a, b);
}
