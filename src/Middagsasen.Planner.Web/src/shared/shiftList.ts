// Vaktlista for en ressurs: tatte vakter, fylt opp med ledige plasser til
// minimumStaff. Hver rad har en stabil, unik nøkkel til `:key` i `v-for`
// (ledige plasser har id 0 og kan ikke bruke id-en). Nøkkelen ligger ved
// siden av vakta, så den aldri kopieres inn i objekter som sendes til API-et.
import type { ShiftResponse } from "@/types";

// Ledig plass i vaktlista (fylles opp til minimumStaff).
export interface VacantShift {
  id: number;
  user: null;
  comment: null;
}

export type ShiftListItem = ShiftResponse | VacantShift;

export interface ShiftListEntry {
  key: string;
  shift: ShiftListItem;
}

export function createShiftList(resource: {
  id: number;
  minimumStaff: number;
  shifts: ShiftResponse[];
}): ShiftListEntry[] {
  const list: ShiftListEntry[] = resource.shifts.map((shift) => ({
    key: `shift-${shift.id}`,
    shift,
  }));
  const neededStaff = resource.minimumStaff - resource.shifts.length;
  for (let i = 0; i < neededStaff; i += 1) {
    list.push({
      key: `empty-${resource.id}-${i}`,
      shift: { id: 0, user: null, comment: null },
    });
  }
  return list;
}
