// Vakt for «bare siste henting teller». Hver henting tar et token med
// start(); når svaret kommer, sjekkes isCurrent(token) før staten oppdateres.
// En nyere start() eller cancel() gjør alle tidligere tokens utdaterte, så
// svar fra eldre hentinger (f.eks. for en vakttype som ikke lenger er valgt)
// forkastes.

export type LatestToken = number;

export interface LatestGuard {
  // Starter en ny henting og gjør alle tidligere utdaterte.
  start(): LatestToken;
  // Sann hvis tokenet er fra siste start() og ingen cancel() har skjedd etter.
  isCurrent(token: LatestToken): boolean;
  // Gjør alle utstedte tokens utdaterte uten å starte en ny henting.
  cancel(): void;
}

export function createLatest(): LatestGuard {
  let current = 0;
  return {
    start: () => ++current,
    isCurrent: (token) => token === current,
    cancel: () => {
      current += 1;
    },
  };
}
