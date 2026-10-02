import { describe, it, expect } from "vitest";
import { applyApproverResults, localApprovers } from "src/shared/approvers";
import type { CompetencyApproverResponse } from "src/types";

function approver(id: number, userId: number): CompetencyApproverResponse {
  return { id, userId, fullName: `User ${userId}` };
}

describe("localApprovers", () => {
  it("returns approvers without id", () => {
    const saved = approver(1, 10);
    const local = approver(0, 11);
    expect(localApprovers([saved, local])).toEqual([local]);
  });
});

describe("applyApproverResults", () => {
  it("replaces successful approvers and keeps failed as local", () => {
    const saved = approver(1, 10);
    const ok = approver(0, 11);
    const bad = approver(0, 12);
    const result = applyApproverResults(
      [saved, ok, bad],
      [ok, bad],
      [
        { status: "fulfilled", value: approver(5, 11) },
        { status: "rejected", reason: new Error("x") },
      ]
    );
    expect(result.failed).toBe(1);
    expect(result.approvers).toEqual([saved, approver(5, 11), bad]);
    expect(result.approvers[2]).toBe(bad);
  });

  it("reports no failures when all succeed", () => {
    const a = approver(0, 11);
    const result = applyApproverResults(
      [a],
      [a],
      [{ status: "fulfilled", value: approver(7, 11) }]
    );
    expect(result).toEqual({ approvers: [approver(7, 11)], failed: 0 });
  });
});
