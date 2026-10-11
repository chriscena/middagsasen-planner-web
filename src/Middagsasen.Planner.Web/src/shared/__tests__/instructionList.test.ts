import { describe, it, expect } from "vitest";
import {
  acknowledgedCount,
  acknowledgementCaption,
  groupInstructions,
  sortInstructionFiles,
} from "@/shared/instructionList";
import type { FileInfoResponse, InstructionGroupResponse } from "@/types";

function file(
  id: number,
  description: string,
  acknowledgedAt: string | null = null
): FileInfoResponse {
  return {
    id,
    resourceTypeId: 1,
    fileName: `instruks-${id}.pdf`,
    description,
    created: "2026-10-01T10:00:00Z",
    createdBy: "Admin",
    updated: "2026-10-01T10:00:00Z",
    updatedBy: "Admin",
    mimeType: "application/pdf",
    requiresAcknowledgement: true,
    isMandatory: false,
    acknowledgedAt,
  };
}

function group(
  resourceTypeId: number,
  resourceTypeName: string,
  appliesToMe: boolean,
  files: FileInfoResponse[] = []
): InstructionGroupResponse {
  return { resourceTypeId, resourceTypeName, appliesToMe, files };
}

describe("sortInstructionFiles", () => {
  it("puts unread first, then sorts by description", () => {
    const sorted = sortInstructionFiles([
      file(1, "Brann", "2026-10-02T10:00:00Z"),
      file(2, "Åpning"),
      file(3, "Avslutning", "2026-10-02T10:00:00Z"),
      file(4, "Heis"),
    ]);
    expect(sorted.map((f) => f.id)).toEqual([4, 2, 3, 1]);
  });

  it("does not mutate the input", () => {
    const files = [file(1, "B"), file(2, "A")];
    sortInstructionFiles(files);
    expect(files.map((f) => f.id)).toEqual([1, 2]);
  });
});

describe("groupInstructions", () => {
  it("splits into mine and others, each sorted by name", () => {
    const sections = groupInstructions([
      group(1, "Storheis", false),
      group(2, "Kiosk", true),
      group(3, "Barneheis", true),
      group(4, "Ørnevakt", false),
      group(5, "Skiutleie", false),
    ]);
    expect(sections.mine.map((g) => g.resourceTypeName)).toEqual([
      "Barneheis",
      "Kiosk",
    ]);
    expect(sections.others.map((g) => g.resourceTypeName)).toEqual([
      "Skiutleie",
      "Storheis",
      "Ørnevakt",
    ]);
  });

  it("sorts files within each group without mutating the store data", () => {
    const original = group(1, "Kiosk", true, [
      file(1, "Kasse", "2026-10-02T10:00:00Z"),
      file(2, "Hygiene"),
    ]);
    const sections = groupInstructions([original]);
    expect(sections.mine[0]?.files.map((f) => f.id)).toEqual([2, 1]);
    expect(original.files.map((f) => f.id)).toEqual([1, 2]);
  });

  it("returns empty sections for no groups", () => {
    expect(groupInstructions([])).toEqual({ mine: [], others: [] });
  });
});

describe("acknowledgementCaption", () => {
  it("counts acknowledged files out of all files", () => {
    const g = group(1, "Kiosk", true, [
      file(1, "A", "2026-10-02T10:00:00Z"),
      file(2, "B"),
      file(3, "C", "2026-10-03T10:00:00Z"),
    ]);
    expect(acknowledgedCount(g)).toBe(2);
    expect(acknowledgementCaption(g)).toBe("2 av 3 kvittert");
  });

  it("handles a group without files", () => {
    expect(acknowledgementCaption(group(1, "Kiosk", true))).toBe(
      "0 av 0 kvittert"
    );
  });
});
