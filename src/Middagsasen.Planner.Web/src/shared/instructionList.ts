// Rekkefølge og tellere for Instrukser-siden: vakttypene deles i «Gjelder
// deg» og «Andre vakttyper», begge sortert alfabetisk; instruksene i hver
// vakttype vises med uleste først, deretter alfabetisk på beskrivelse.
import type { FileInfoResponse, InstructionGroupResponse } from "@/types";

export interface InstructionSections {
  // Vakttyper brukeren har vakt på (appliesToMe).
  mine: InstructionGroupResponse[];
  others: InstructionGroupResponse[];
}

const collator = new Intl.Collator("nb");

function byName(
  a: InstructionGroupResponse,
  b: InstructionGroupResponse
): number {
  return collator.compare(a.resourceTypeName, b.resourceTypeName);
}

// Uleste først, så alfabetisk på beskrivelse. Returnerer en ny liste.
export function sortInstructionFiles(
  files: readonly FileInfoResponse[]
): FileInfoResponse[] {
  return [...files].sort((a, b) => {
    const unreadA = a.acknowledgedAt ? 1 : 0;
    const unreadB = b.acknowledgedAt ? 1 : 0;
    if (unreadA !== unreadB) return unreadA - unreadB;
    return collator.compare(a.description, b.description);
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
