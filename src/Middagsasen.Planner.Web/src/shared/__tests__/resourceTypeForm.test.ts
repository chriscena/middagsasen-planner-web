import { describe, it, expect } from "vitest";
import { reactive } from "vue";
import {
  newEditableTrainer,
  toEditableResourceType,
  toResourceTypeRequest,
} from "@/shared/resourceTypeForm";
import type { ResourceTypeResponse } from "@/types";

function resourceType(): ResourceTypeResponse {
  return {
    id: 3,
    name: "Heis",
    defaultShiftCount: 2,
    hasTraining: true,
    notificationMessage: null,
    trainers: [{ id: 7, userId: 70, fullName: "Kari", phoneNo: "123" }],
    files: [
      {
        id: 9,
        resourceTypeId: 3,
        fileName: "a.pdf",
        description: "A",
        created: "2024-01-01",
        createdBy: "x",
        updated: "2024-01-01",
        updatedBy: "x",
        mimeType: "application/pdf",
      },
    ],
  };
}

describe("toEditableResourceType", () => {
  it("copies nested reactive data without sharing objects", () => {
    const source = reactive(resourceType());
    const copy = toEditableResourceType(source);
    // structuredClone ville kastet DataCloneError på nestede proxies.
    expect(() => structuredClone(copy)).not.toThrow();
    copy.trainers[0]!.isDeleted = true;
    copy.files![0]!.description = "B";
    expect(source.trainers[0]).not.toHaveProperty("isDeleted");
    expect(source.files[0]!.description).toBe("A");
  });

  it("gives trainers a client key from the id", () => {
    const copy = toEditableResourceType(resourceType());
    expect(copy.trainers[0]!.clientKey).toBe("id-7");
  });
});

describe("newEditableTrainer", () => {
  it("creates unique client keys", () => {
    const user = { id: 1, fullName: "Ola", phoneNo: "1" };
    const a = newEditableTrainer(user);
    const b = newEditableTrainer(user);
    expect(a.id).toBe(0);
    expect(a.clientKey).not.toBe(b.clientKey);
  });
});

describe("toResourceTypeRequest", () => {
  it("maps only request fields", () => {
    const form = toEditableResourceType(resourceType());
    form.defaultShiftCount = "4";
    form.trainers.push(newEditableTrainer({ id: 2, phoneNo: "2" }));
    expect(toResourceTypeRequest(form)).toEqual({
      name: "Heis",
      defaultShiftCount: 4,
      notificationMessage: null,
      trainers: [
        { id: 7, userId: 70, isDeleted: false },
        { id: 0, userId: 2, isDeleted: false },
      ],
    });
  });
});
