// «Har lest til bunnen»-regelen for vedleggsviseren (AttachmentViewerDialog).
//
// Kvitteringsknappen er sperret til bunnen av dokumentet har vært synlig.
// Viseren legger et usynlig element (sentinel) under siste side og observerer
// det med IntersectionObserver; denne modulen er den rene logikken rundt, så
// den kan testes uten DOM.

const PDF_MIME_TYPE = "application/pdf";

/**
 * Om dokumentet må blas til bunnen før det regnes som lest. Bare PDF-er har
 * flere sider; bilder (og alt annet) regnes som lest straks de er vist.
 */
export function requiresScrollToEnd(mimeType: string): boolean {
  return mimeType.toLowerCase() === PDF_MIME_TYPE;
}

/**
 * Ny verdi for «bunnen er nådd» etter en runde observasjoner av sentinelen.
 * Klebrig: har bunnen vært synlig én gang, forblir den nådd selv om brukeren
 * blar opp igjen (det er lesingen som teller, ikke posisjonen nå).
 */
export function reachedEnd(
  previouslyReached: boolean,
  entries: ReadonlyArray<Pick<IntersectionObserverEntry, "isIntersecting">>
): boolean {
  return previouslyReached || entries.some((entry) => entry.isIntersecting);
}
