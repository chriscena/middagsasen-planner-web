// «Har lest til bunnen»-regelen for vedleggsviseren (AttachmentViewerDialog).
//
// Kvitteringsknappen er sperret til bunnen av dokumentet har vært synlig.
// Viseren legger et usynlig element (sentinel) under siste side og observerer
// det med IntersectionObserver; denne modulen er den rene logikken rundt, så
// den kan testes uten DOM.

const PDF_MIME_TYPE = "application/pdf";
const PDF_EXTENSION = ".pdf";
const IMAGE_MIME_PREFIX = "image/";

/**
 * Hvordan viseren behandler vedlegget:
 * - "pdf": alle sider rendres med pdf.js; lest når bunnen har vært synlig.
 * - "image": vises med q-img; lest straks det er vist.
 * - "other": kan ikke vises; brukeren laster ned, og det regnes som lest når
 *   nedlastingen er startet.
 */
export type AttachmentKind = "pdf" | "image" | "other";

/**
 * Bestemmer typen fra MIME-typen, med filnavnet som reserve for PDF (noen
 * opplastinger kommer med generisk MIME, f.eks. application/octet-stream).
 */
export function attachmentKind(
  mimeType: string,
  fileName: string
): AttachmentKind {
  const mime = mimeType.trim().toLowerCase();
  if (mime === PDF_MIME_TYPE) return "pdf";
  if (fileName.trim().toLowerCase().endsWith(PDF_EXTENSION)) return "pdf";
  if (mime.startsWith(IMAGE_MIME_PREFIX)) return "image";
  return "other";
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
