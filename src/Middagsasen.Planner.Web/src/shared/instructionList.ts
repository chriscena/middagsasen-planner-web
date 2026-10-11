// Rekkefølge, tellere og tekster for instrukser: vakttypene deles i «Gjelder
// deg» og «Andre vakttyper», begge sortert alfabetisk; instruksene i hver
// vakttype vises med uleste først, deretter alfabetisk på beskrivelse. Samme
// rekkefølge brukes i «Nyttig info» på oppgaven.
import { compareNb } from "@/shared/sorting";
import type { FileInfoResponse, InstructionGroupResponse } from "@/types";

export interface InstructionSections {
  // Vakttyper brukeren har vakt på (appliesToMe).
  mine: InstructionGroupResponse[];
  others: InstructionGroupResponse[];
}

function byName(
  a: InstructionGroupResponse,
  b: InstructionGroupResponse
): number {
  return compareNb(a.resourceTypeName, b.resourceTypeName);
}

// Uleste først, så alfabetisk på beskrivelse. Returnerer en ny liste.
export function sortInstructionFiles(
  files: readonly FileInfoResponse[]
): FileInfoResponse[] {
  return [...files].sort((a, b) => {
    const unreadA = a.acknowledgedAt ? 1 : 0;
    const unreadB = b.acknowledgedAt ? 1 : 0;
    if (unreadA !== unreadB) return unreadA - unreadB;
    return compareNb(a.description, b.description);
  });
}

// Deler i seksjoner og sorterer gruppene og filene. Gruppene i resultatet er
// kopier med sorterte filer; store-ens data endres ikke.
export function groupInstructions(
  groups: readonly InstructionGroupResponse[]
): InstructionSections {
  const sorted = groups
    .map((group) => ({ ...group, files: sortInstructionFiles(group.files) }))
    .sort(byName);
  return {
    mine: sorted.filter((group) => group.appliesToMe),
    others: sorted.filter((group) => !group.appliesToMe),
  };
}

export function acknowledgedCount(group: InstructionGroupResponse): number {
  return group.files.filter((file) => !!file.acknowledgedAt).length;
}

// Undertekst på vakttypen, f.eks. «2 av 3 kvittert».
export function acknowledgementCaption(
  group: InstructionGroupResponse
): string {
  return `${acknowledgedCount(group)} av ${group.files.length} kvittert`;
}

// «1 ulest instruks» / «3 uleste instrukser»; tom streng når ingen er uleste.
export function formatUnreadInstructions(count: number): string {
  if (count <= 0) return "";
  return count === 1 ? "1 ulest instruks" : `${count} uleste instrukser`;
}

// Tilgjengelig navn på profilknappen, med antall uleste når det finnes noen.
export function profileMenuTitle(count: number): string {
  const unread = formatUnreadInstructions(count);
  return unread ? `Din brukerinfo – ${unread}` : "Din brukerinfo";
}
