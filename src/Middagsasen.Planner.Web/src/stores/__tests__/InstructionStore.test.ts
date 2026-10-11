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

import {
  reloadInstructionsInBackground,
  useInstructionStore,
} from "@/stores/InstructionStore";

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

// Promise som testen løser selv, for å styre rekkefølgen på svarene.
function deferred<T>(): {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason: unknown) => void;
} {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

// Lar alle ventende promise-callbacks kjøre (svarene som er løst).
function flushPromises(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
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

    it("deler en pågående henting: parallelle kall gir én GET", async () => {
      const first = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get.mockReturnValueOnce(first.promise);
      const groups = [group(1, true, [file(10, 1)])];

      const a = store.getMyInstructions();
      const b = store.getMyInstructions();
      expect(store.loading).toBe(true);
      first.resolve({ data: groups });
      await Promise.all([a, b]);

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(store.groups).toEqual(groups);
      expect(store.loading).toBe(false);
    });

    it("henter på nytt når forrige henting er ferdig", async () => {
      mockApi.get.mockResolvedValue({ data: [] });

      await store.getMyInstructions();
      await store.getMyInstructions();

      expect(mockApi.get).toHaveBeenCalledTimes(2);
    });
  });

  describe("reloadInstructions", () => {
    it("lar bare den siste startede hentingen sette groups", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);
      const newerGroups = [group(2, true, [file(20, 2)])];

      const a = store.reloadInstructions();
      const b = store.reloadInstructions();
      newer.resolve({ data: newerGroups });
      await b;
      expect(store.groups).toEqual(newerGroups);
      expect(store.loading).toBe(false);

      older.resolve({ data: [group(1, true, [file(10, 1)])] });
      await a;

      expect(store.groups).toEqual(newerGroups);
    });

    it("holder loading sann til den siste hentingen er ferdig", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const a = store.reloadInstructions();
      const b = store.reloadInstructions();
      older.resolve({ data: [] });
      await flushPromises();
      expect(store.loading).toBe(true);

      newer.resolve({ data: [] });
      await Promise.all([a, b]);
      expect(store.loading).toBe(false);
    });

    it("starter ny henting selv om en alt pågår", async () => {
      const first = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(first.promise)
        .mockResolvedValueOnce({ data: [] });

      const a = store.getMyInstructions();
      const b = store.reloadInstructions();
      first.resolve({ data: [] });
      await Promise.all([a, b]);

      expect(mockApi.get).toHaveBeenCalledTimes(2);
    });

    it("lar en kaller av getMyInstructions lykkes når den gamle hentingen feiler, men den nye lykkes", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);
      const newerGroups = [group(2, true, [file(20, 2)])];

      const pending = store.getMyInstructions();
      const reload = store.reloadInstructions();
      older.reject(new Error("Network Error"));
      newer.resolve({ data: newerGroups });

      await expect(pending).resolves.toBeUndefined();
      await expect(reload).resolves.toBeUndefined();
      expect(store.groups).toEqual(newerGroups);
      expect(store.loading).toBe(false);
    });

    it("bruker et vellykket eldre svar, men lar den nyeste hentingen avgjøre utfallet", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);
      const olderGroups = [group(1, true, [file(10, 1)])];

      let settled = false;
      const pending = store.getMyInstructions().finally(() => {
        settled = true;
      });
      const reload = store.reloadInstructions();
      older.resolve({ data: olderGroups });
      await flushPromises();
      // Dataene fra den eldre vises mens den nyeste pågår.
      expect(settled).toBe(false);
      expect(store.groups).toEqual(olderGroups);
      expect(store.loading).toBe(true);

      newer.reject(new Error("Network Error"));

      // Den nyeste feilet: kallerne får feilen, men dataene beholdes.
      await expect(pending).rejects.toThrow("Network Error");
      await expect(reload).rejects.toThrow("Network Error");
      expect(store.groups).toEqual(olderGroups);
      expect(store.unacknowledgedCount).toBe(1);
      expect(store.loading).toBe(false);
    });

    it("gir den nye hentingens feil, men bruker et eldre svar som kommer etterpå", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);
      const olderGroups = [group(1, true, [file(10, 1)])];

      const pending = store.getMyInstructions();
      const reload = store.reloadInstructions();
      newer.reject(new Error("Network Error"));
      await expect(reload).rejects.toThrow("Network Error");
      older.resolve({ data: olderGroups });

      await expect(pending).rejects.toThrow("Network Error");
      // Ingen nyere svar er brukt, så det eldre er det beste vi har.
      expect(store.groups).toEqual(olderGroups);
    });

    it("lar et vellykket eldre svar bli stående når en ny reload feiler", async () => {
      const groups = [group(1, true, [file(10, 1)])];
      mockApi.get
        .mockResolvedValueOnce({ data: groups })
        .mockRejectedValueOnce(new Error("Network Error"));

      await store.getMyInstructions();
      await expect(store.reloadInstructions()).rejects.toThrow("Network Error");

      expect(store.groups).toEqual(groups);
    });

    it("getMyInstructions under en reload deler den nyeste hentingen", async () => {
      const older = deferred<{ data: InstructionGroupResponse[] }>();
      const newer = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const a = store.reloadInstructions();
      const b = store.reloadInstructions();
      const c = store.getMyInstructions();
      older.reject(new Error("Network Error"));
      newer.resolve({ data: [] });

      await expect(Promise.all([a, b, c])).resolves.toBeDefined();
      expect(mockApi.get).toHaveBeenCalledTimes(2);
    });
  });

  describe("reloadInstructionsInBackground", () => {
    it("starter en ny henting", async () => {
      const groups = [group(1, true, [file(10, 1)])];
      mockApi.get.mockResolvedValue({ data: groups });

      reloadInstructionsInBackground();
      expect(store.loading).toBe(true);
      await store.getMyInstructions();

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(store.groups).toEqual(groups);
    });

    it("logger feil i stedet for å kaste dem", async () => {
      const error = new Error("Network Error");
      const logged = new Promise<void>((resolve) => {
        vi.spyOn(console, "error").mockImplementation(() => resolve());
      });
      mockApi.get.mockRejectedValue(error);

      expect(() => reloadInstructionsInBackground()).not.toThrow();
      await logged;

      expect(console.error).toHaveBeenCalledWith(error);
      expect(store.loading).toBe(false);
    });
  });

  describe("reset", () => {
    it("tømmer listen og forkaster svar som er underveis", async () => {
      store.groups = [group(1, true, [file(10, 1)])];
      const pending = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get.mockReturnValueOnce(pending.promise);
      const request = store.reloadInstructions();

      store.reset();
      expect(store.groups).toEqual([]);
      expect(store.loading).toBe(false);
      expect(store.unacknowledgedCount).toBe(0);

      pending.resolve({ data: [group(2, true, [file(20, 2)])] });
      await request;

      expect(store.groups).toEqual([]);
      expect(store.loading).toBe(false);
    });

    it("forkaster svaret for en kaller som venter under reset, uten feil", async () => {
      const pending = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get.mockReturnValueOnce(pending.promise);
      const request = store.getMyInstructions();

      store.reset();
      pending.resolve({ data: [group(1, true, [file(10, 1)])] });

      await expect(request).resolves.toBeUndefined();
      expect(store.groups).toEqual([]);
      expect(store.unacknowledgedCount).toBe(0);
    });

    it("forkaster en feil fra en henting som er nullstilt", async () => {
      const pending = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get.mockReturnValueOnce(pending.promise);
      const request = store.getMyInstructions();

      store.reset();
      pending.reject(new Error("Network Error"));

      await expect(request).resolves.toBeUndefined();
      expect(store.loading).toBe(false);
    });

    it("bytte av bruker: ny henting etter reset deles ikke med den gamle, og den gamles svar forkastes", async () => {
      const forA = deferred<{ data: InstructionGroupResponse[] }>();
      const forB = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(forA.promise)
        .mockReturnValueOnce(forB.promise);
      const groupsB = [group(2, true, [file(20, 2)])];

      const a = store.getMyInstructions();
      store.reset();
      const b = store.getMyInstructions();
      expect(mockApi.get).toHaveBeenCalledTimes(2);

      forB.resolve({ data: groupsB });
      await b;
      forA.resolve({ data: [group(1, true, [file(10, 1)])] });
      await a;

      expect(store.groups).toEqual(groupsB);
    });

    it("lar en ny henting etter reset virke som normalt", async () => {
      store.reset();
      const groups = [group(1, true, [file(10, 1)])];
      mockApi.get.mockResolvedValue({ data: groups });

      await store.getMyInstructions();

      expect(store.groups).toEqual(groups);
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

    it("overstyrer en henting som alt var underveis, så status ikke blir fra før kvitteringen", async () => {
      const stale = deferred<{ data: InstructionGroupResponse[] }>();
      mockApi.get
        .mockReturnValueOnce(stale.promise)
        .mockResolvedValueOnce({ data: [group(1, true, [acknowledged])] });
      mockApi.post.mockResolvedValue({ data: acknowledged });

      const pending = store.getMyInstructions();
      await store.acknowledge(1, 10);
      expect(store.unacknowledgedCount).toBe(0);

      stale.resolve({ data: [group(1, true, [file(10, 1)])] });
      await pending;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(store.unacknowledgedCount).toBe(0);
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
