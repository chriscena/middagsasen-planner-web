import type { UserResponse } from "@/types";

/**
 * Filtrerer brukere på navn (del av fullt navn, uten hensyn til store og små
 * bokstaver). Tomt søk gir alle. Brukes av søkbare brukervalg (q-select med
 * `use-input`).
 */
export function filterUsersByName(
  users: UserResponse[],
  search: string | null | undefined
): UserResponse[] {
  const needle = (search ?? "").trim().toLowerCase();
  if (!needle) return users;
  return users.filter((u) => (u.fullName ?? "").toLowerCase().includes(needle));
}
