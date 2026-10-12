import { describe, it, expect } from "vitest";
import { filterUsersByName } from "@/shared/users";
import type { UserResponse } from "@/types";

function user(id: number, fullName: string | null): UserResponse {
  return {
    id,
    phoneNo: "",
    fullName,
    isAdmin: false,
    isHidden: false,
    shiftReminders: false,
    staffingAlerts: false,
    trainings: [],
  };
}

const users = [user(1, "Ola Nordmann"), user(2, "Kari Hansen"), user(3, null)];

describe("filterUsersByName", () => {
  it("gir alle brukere ved tomt søk", () => {
    expect(filterUsersByName(users, "")).toBe(users);
    expect(filterUsersByName(users, null)).toBe(users);
    expect(filterUsersByName(users, "  ")).toBe(users);
  });

  it("finner del av navnet uten hensyn til store og små bokstaver", () => {
    expect(filterUsersByName(users, "nord").map((u) => u.id)).toEqual([1]);
    expect(filterUsersByName(users, "HAN").map((u) => u.id)).toEqual([2]);
  });

  it("gir tom liste uten treff", () => {
    expect(filterUsersByName(users, "per")).toEqual([]);
  });
});
