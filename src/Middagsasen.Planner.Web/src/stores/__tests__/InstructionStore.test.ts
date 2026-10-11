import { vi, describe, it, expect, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { FileInfoResponse, InstructionGroupResponse } from "@/types";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock("@/boot/axios", () => ({
  api: mockApi,
}));

import { useInstructionStore } from "@/stores/InstructionStore";

function file(
  id: number,
  resourceTypeId: number,
  overrides: Partial<FileInfoResponse> = {}
): FileInfoResponse {
  return {
    id,
    resourceTypeId,
    fileName: `instruks-${id}.pdf`,
    description: `Instruks ${id}`,
    created: "2026-10-01T10:00:00Z",
    createdBy: "Admin",
    updated: "2026-10-01T10:00:00Z",
    updatedBy: "Admin",
    mimeType: "application/pdf",
    requiresAcknowledgement: true,
    isMandatory: false,
    acknowledgedAt: null,
    archived: null,
    ...overrides,
  };
}

function group(
  resourceTypeId: number,
  appliesToMe: boolean,
  files: FileInfoResponse[]
): InstructionGroupResponse {
  return {
    resourceTypeId,
    resourceTypeName: `Vakttype ${resourceTypeId}`,
    appliesToMe,
    files,
  };
}

describe("InstructionStore", () => {
  let store: ReturnType<typeof useInstructionStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useInstructionStore();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("getMyInstructions", () => {
    it("henter gruppene og legger dem i state", async () => {
      const groups = [group(1, true, [file(10, 1)])];
      mockApi.get.mockResolvedValue({ data: groups });

      await store.getMyInstructions();

      expect(mockApi.get).toHaveBeenCalledWith("/api/me/instructions");
      expect(store.groups).toEqual(groups);
      expect(store.loading).toBe(false);
    });

    it("nullstiller loading også når hentingen feiler", async () => {
      mockApi.get.mockRejectedValue(new Error("Network Error"));

      await expect(store.getMyInstructions()).rejects.toThrow("Network Error");

      expect(store.loading).toBe(false);
    });
  });

  describe("unacknowledgedCount", () => {
    it("teller uleste instrukser bare i grupper som gjelder brukeren", () => {
      store.groups = [
        group(1, true, [
          file(10, 1),
          file(11, 1, { acknowledgedAt: "2026-10-02T08:00:00Z" }),
          file(12, 1),
        ]),
        group(2, false, [file(20, 2), file(21, 2)]),
        group(3, true, [
          file(30, 3, { acknowledgedAt: "2026-10-02T08:00:00Z" }),
        ]),
      ];

      expect(store.unacknowledgedCount).toBe(2);
    });

    it("er null uten instrukser", () => {
      expect(store.unacknowledgedCount).toBe(0);
    });
  });

  describe("acknowledge", () => {
    const acknowledged = file(10, 1, {
      acknowledgedAt: "2026-10-05T12:00:00Z",
    });

    it("poster uten body når kommentaren mangler, og returnerer filen", async () => {
      mockApi.post.mockResolvedValue({ data: acknowledged });
      mockApi.get.mockResolvedValue({ data: [] });

      const result = await store.acknowledge(1, 10);

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/resourcetypes/1/files/10/acknowledgements",
        undefined
      );
      expect(result).toEqual(acknowledged);
    });

    it("poster uten body når kommentaren er tom eller bare mellomrom", async () => {
      mockApi.post.mockResolvedValue({ data: acknowledged });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.acknowledge(1, 10, "");
      await store.acknowledge(1, 10, "   ");

      expect(mockApi.post).toHaveBeenNthCalledWith(
        1,
        "/api/resourcetypes/1/files/10/acknowledgements",
        undefined
      );
      expect(mockApi.post).toHaveBeenNthCalledWith(
        2,
        "/api/resourcetypes/1/files/10/acknowledgements",
        undefined
      );
    });

    it("sender kommentaren (trimmet) i body når den er satt", async () => {
      mockApi.post.mockResolvedValue({ data: acknowledged });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.acknowledge(1, 10, "  Lest og forstått  ");

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/resourcetypes/1/files/10/acknowledgements",
        { comment: "Lest og forstått" }
      );
    });

    it("henter instruksene på nytt etter kvittering, så telleren oppdateres", async () => {
      store.groups = [group(1, true, [file(10, 1), file(11, 1)])];
      expect(store.unacknowledgedCount).toBe(2);

      mockApi.post.mockResolvedValue({ data: acknowledged });
      mockApi.get.mockResolvedValue({
        data: [group(1, true, [acknowledged, file(11, 1)])],
      });

      await store.acknowledge(1, 10);

      expect(mockApi.get).toHaveBeenCalledWith("/api/me/instructions");
      expect(store.unacknowledgedCount).toBe(1);
      expect(store.groups[0]?.files[0]?.acknowledgedAt).toBe(
        "2026-10-05T12:00:00Z"
      );
    });

    it("returnerer filen selv om ny henting av listen feiler", async () => {
      vi.spyOn(console, "error").mockImplementation(() => {});
      mockApi.post.mockResolvedValue({ data: acknowledged });
      mockApi.get.mockRejectedValue(new Error("Network Error"));

      const result = await store.acknowledge(1, 10);

      expect(result).toEqual(acknowledged);
      expect(console.error).toHaveBeenCalled();
    });

    it("kaster videre når kvitteringen avvises, uten å hente listen", async () => {
      const error = { response: { status: 400, data: { detail: "Nei" } } };
      mockApi.post.mockRejectedValue(error);

      await expect(store.acknowledge(1, 10)).rejects.toBe(error);

      expect(mockApi.get).not.toHaveBeenCalled();
    });
  });
});
