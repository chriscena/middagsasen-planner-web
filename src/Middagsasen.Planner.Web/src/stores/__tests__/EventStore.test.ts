import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  EventResponse,
  ResourceResponse,
  ShiftResponse,
  ShiftResult,
  TrainingResponse,
} from "@/types";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
}));

vi.mock("@/boot/axios", () => ({
  api: mockApi,
}));

import { useEventStore } from "@/stores/EventStore";

// Kun feltene storen bruker (id og startTime) er relevante her.
function event(id: number, startTime: string): EventResponse {
  return { id, startTime } as EventResponse;
}

const CURRENT_USER_ID = 1;
const OTHER_USER_ID = 2;
const TYPE_ID = 5;

function shift(
  id: number,
  resourceId: number,
  userId: number,
  overrides: Partial<ShiftResponse> = {}
): ShiftResponse {
  return {
    id,
    eventResourceId: resourceId,
    user: { id: userId, phoneNumber: String(userId), trainings: [] },
    needsTraining: false,
    isMine: userId === CURRENT_USER_ID,
    canEdit: false,
    canWithdraw: false,
    canConfirmTraining: false,
    ...overrides,
  };
}

function resource(
  id: number,
  shifts: ShiftResponse[],
  overrides: Partial<ResourceResponse> = {}
): ResourceResponse {
  return {
    id,
    eventId: 1,
    resourceType: { id: TYPE_ID } as ResourceResponse["resourceType"],
    startTime: "2026-10-05T10:00",
    endTime: "2026-10-05T16:00",
    minimumStaff: 2,
    shifts,
    messages: [],
    competencyWarnings: [],
    isMissingStaff: true,
    isFull: false,
    isPast: false,
    canSignUp: true,
    mustAnswerTraining: true,
    ...overrides,
  };
}

function training(
  trainingComplete: boolean,
  userId: number = CURRENT_USER_ID
): TrainingResponse {
  return { id: 100, userId, resourceTypeId: TYPE_ID, trainingComplete };
}

function result(
  updated: ResourceResponse,
  changedTraining: TrainingResponse | null = null
): ShiftResult {
  return { resource: updated, changedTraining, warnings: [] };
}

function eventWith(id: number, resources: ResourceResponse[]): EventResponse {
  return { id, resources } as EventResponse;
}

describe("EventStore", () => {
  let store: ReturnType<typeof useEventStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useEventStore();
    // Nullstiller også mockResolvedValue o.l. fra forrige test.
    vi.resetAllMocks();
    vi.restoreAllMocks();
  });

  describe("applyShiftResult", () => {
    it("oppdaterer ressursen i cachen uten å bytte objektet", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      // Slik en dialog holder på ressursen (selectedResource).
      const held = store.events[0]!.resources[0]!;

      await store.applyShiftResult(
        result(
          resource(10, [shift(1, 10, CURRENT_USER_ID)], {
            isMissingStaff: false,
            canSignUp: false,
          })
        )
      );

      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.shifts.map((s) => s.id)).toEqual([1]);
      expect(held.isMissingStaff).toBe(false);
      expect(held.canSignUp).toBe(false);
    });

    it("henter ikke events på nytt uten changedTraining", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };

      await store.applyShiftResult(result(resource(10, [])));

      expect(mockApi.get).not.toHaveBeenCalled();
    });

    it("henter perioden kalenderen viser på nytt ved changedTraining", async () => {
      mockApi.get.mockResolvedValueOnce({ data: [] });
      await store.getEventsForDates("2026-10-05", "2026-10-11");
      const refetched = [
        eventWith(1, [resource(10, [], { canSignUp: false })]),
      ];
      mockApi.get.mockResolvedValueOnce({ data: refetched });

      await store.applyShiftResult(
        result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
      );

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(mockApi.get).toHaveBeenLastCalledWith(
        "/api/events?start=2026-10-05&end=2026-10-12"
      );
      expect(store.events).toEqual(refetched);
    });

    it("henter ikke på nytt når ingen periode er hentet", async () => {
      store.events = [eventWith(1, [resource(10, [])])];

      await store.applyShiftResult(
        result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
      );

      expect(mockApi.get).not.toHaveBeenCalled();
      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });

    it("feil ved ny henting kaster ikke, og ressursen er oppdatert", async () => {
      vi.spyOn(console, "error").mockImplementation(() => {});
      store.events = [eventWith(1, [resource(10, [])])];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      mockApi.get.mockRejectedValue(new Error("nettverk"));

      await expect(
        store.applyShiftResult(
          result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
        )
      ).resolves.toBeUndefined();

      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });
  });

  describe("saveShift", () => {
    it("ledig plass poster til ressursen og legger svaret i cachen", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const response = result(resource(10, [shift(1, 10, OTHER_USER_ID)]));
      mockApi.post.mockResolvedValue({ data: response });

      const returned = await store.saveShift({
        resourceId: 10,
        shiftId: null,
        userId: OTHER_USER_ID,
        comment: "Hei",
        trainingCompleted: false,
      });

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/shifts", {
        userId: OTHER_USER_ID,
        comment: "Hei",
        trainingCompleted: false,
      });
      expect(mockApi.put).not.toHaveBeenCalled();
      expect(returned).toBe(response);
      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });

    it("ledig plass sender alltid comment, men utelater andre felt som ikke er satt", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.post.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });

      await store.saveShift({ resourceId: 10, shiftId: null, comment: null });

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/shifts", {
        comment: null,
      });
    });

    it("eksisterende vakt sendes med PUT til vakta", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      const updated = shift(1, 10, OTHER_USER_ID, { comment: "Hei" });
      mockApi.put.mockResolvedValue({ data: result(resource(10, [updated])) });

      await store.saveShift({
        resourceId: 10,
        shiftId: 1,
        userId: OTHER_USER_ID,
        comment: "Hei",
        trainingCompleted: true,
      });

      expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/1", {
        userId: OTHER_USER_ID,
        comment: "Hei",
        trainingCompleted: true,
      });
      expect(mockApi.post).not.toHaveBeenCalled();
      expect(store.events[0]!.resources[0]!.shifts[0]!.comment).toBe("Hei");
    });

    it("PUT sender alltid comment, men utelater andre felt som ikke er satt", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      mockApi.put.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });

      await store.saveShift({ resourceId: 10, shiftId: 1, comment: null });

      expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/1", {
        comment: null,
      });
    });

    it.each([true, false, null])(
      "sender trainingCompleted %s uendret",
      async (trainingCompleted) => {
        store.events = [
          eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
        ];
        mockApi.put.mockResolvedValue({
          data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
        });

        await store.saveShift({
          resourceId: 10,
          shiftId: 1,
          comment: null,
          trainingCompleted,
        });

        expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/1", {
          comment: null,
          trainingCompleted,
        });
      }
    );

    it("changedTraining henter perioden kalenderen viser på nytt", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      const response = result(
        resource(10, [shift(1, 10, OTHER_USER_ID)]),
        training(false, OTHER_USER_ID)
      );
      mockApi.post.mockResolvedValue({ data: response });
      mockApi.get.mockResolvedValue({ data: [] });

      const returned = await store.saveShift({
        resourceId: 10,
        shiftId: null,
        userId: OTHER_USER_ID,
        comment: null,
        trainingCompleted: false,
      });

      expect(returned).toBe(response);
      expect(mockApi.get).toHaveBeenCalledWith(
        "/api/events?start=2026-10-05&end=2026-10-12"
      );
    });

    it("feil propagerer uten å endre cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [shift(1, 10, CURRENT_USER_ID, { comment: "Før" })]),
        ]),
      ];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      const error = new Error("400");
      mockApi.put.mockRejectedValue(error);

      await expect(
        store.saveShift({ resourceId: 10, shiftId: 1, comment: "Etter" })
      ).rejects.toBe(error);

      expect(store.events[0]!.resources[0]!.shifts[0]!.comment).toBe("Før");
      expect(mockApi.get).not.toHaveBeenCalled();
    });
  });

  describe("vaktoperasjoner", () => {
    it("setTraining sender svaret og henter perioden på nytt", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      mockApi.put.mockResolvedValue({
        data: result(
          resource(10, [shift(7, 10, OTHER_USER_ID)]),
          training(true, OTHER_USER_ID)
        ),
      });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.setTraining(7, true);

      expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/7/training", {
        trainingCompleted: true,
      });
      expect(mockApi.get).toHaveBeenCalledWith(
        "/api/events?start=2026-10-05&end=2026-10-12"
      );
    });

    it("withdraw sletter og legger ressursen i cachen", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      mockApi.delete.mockResolvedValue({ data: result(resource(10, [])) });

      await store.withdraw(1);

      expect(mockApi.delete).toHaveBeenCalledWith("/api/shifts/1");
      expect(store.events[0]!.resources[0]!.shifts).toEqual([]);
    });
  });

  describe("addEmptySlot", () => {
    it("poster uten body og legger ressursen med flagg i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [shift(1, 10, CURRENT_USER_ID)], {
            minimumStaff: 1,
            isMissingStaff: false,
            isFull: true,
          }),
        ]),
      ];
      const held = store.events[0]!.resources[0]!;
      const updated = resource(10, [shift(1, 10, CURRENT_USER_ID)], {
        minimumStaff: 2,
        isMissingStaff: true,
        isFull: false,
      });
      mockApi.post.mockResolvedValue({ data: updated });

      const returned = await store.addEmptySlot(10);

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/emptySlots");
      expect(returned).toBe(updated);
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.minimumStaff).toBe(2);
      expect(held.isMissingStaff).toBe(true);
      expect(held.isFull).toBe(false);
    });
  });

  describe("removeEmptySlot", () => {
    it("sletter uten body og legger ressursen med flagg i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [shift(1, 10, CURRENT_USER_ID)], {
            minimumStaff: 2,
            isMissingStaff: true,
            isFull: false,
          }),
        ]),
      ];
      const held = store.events[0]!.resources[0]!;
      const updated = resource(10, [shift(1, 10, CURRENT_USER_ID)], {
        minimumStaff: 1,
        isMissingStaff: false,
        isFull: true,
      });
      mockApi.delete.mockResolvedValue({ data: updated });

      const returned = await store.removeEmptySlot(10);

      expect(mockApi.delete).toHaveBeenCalledWith(
        "/api/resources/10/emptySlots"
      );
      expect(returned).toBe(updated);
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.minimumStaff).toBe(1);
      expect(held.isMissingStaff).toBe(false);
      expect(held.isFull).toBe(true);
    });
  });

  describe("getEventsForDates", () => {
    it("henter events for perioden (slutt er eksklusiv, derfor +1 dag)", async () => {
      const events = [event(1, "2026-10-05T10:00:00")];
      mockApi.get.mockResolvedValue({ data: events });

      await store.getEventsForDates("2026-10-05", "2026-10-11");

      expect(mockApi.get).toHaveBeenCalledWith(
        "/api/events?start=2026-10-05&end=2026-10-12"
      );
      expect(store.events).toEqual(events);
    });

    it("forkaster svar på en eldre forespørsel som kommer etter en nyere", async () => {
      let resolveOld!: (value: { data: EventResponse[] }) => void;
      const oldEvents = [event(1, "2026-09-28T10:00:00")];
      const newEvents = [event(2, "2026-10-05T10:00:00")];
      mockApi.get
        .mockImplementationOnce(
          () => new Promise((resolve) => (resolveOld = resolve))
        )
        .mockResolvedValueOnce({ data: newEvents });

      const old = store.getEventsForDates("2026-09-28", "2026-10-04");
      await store.getEventsForDates("2026-10-05", "2026-10-11");
      resolveOld({ data: oldEvents });
      await old;

      expect(store.events).toEqual(newEvents);
    });
  });

  describe("refreshEventResources", () => {
    it("henter arrangementet og legger ressursene i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [], { minimumStaff: 2, isMissingStaff: true }),
        ]),
        eventWith(2, [resource(20, [], { minimumStaff: 3 })]),
      ];
      const held = store.events[0]!.resources[0]!;
      const other = store.events[1]!.resources[0]!;
      const fresh = eventWith(1, [
        resource(10, [shift(1, 10, OTHER_USER_ID)], {
          minimumStaff: 1,
          isMissingStaff: false,
          isFull: true,
        }),
      ]);
      mockApi.get.mockResolvedValue({ data: fresh });

      await store.refreshEventResources(1);

      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.minimumStaff).toBe(1);
      expect(held.shifts).toHaveLength(1);
      expect(held.isMissingStaff).toBe(false);
      expect(held.isFull).toBe(true);
      expect(other.minimumStaff).toBe(3);
    });

    it("kaster videre når hentingen feiler", async () => {
      const error = new Error("500");
      mockApi.get.mockRejectedValue(error);

      await expect(store.refreshEventResources(1)).rejects.toBe(error);
    });
  });

  describe("getEvent", () => {
    it("nullstiller selectedEvent når kallet feiler", async () => {
      store.selectedEvent = event(1, "2026-10-05T10:00:00");
      const error = new Error("500");
      mockApi.get.mockRejectedValue(error);

      await expect(store.getEvent(2)).rejects.toBe(error);

      expect(store.selectedEvent).toBeNull();
    });
  });

  describe("getEventsForDate", () => {
    it("filtrerer på dato fra timestamp", () => {
      store.events = [
        event(1, "2026-10-05T10:00:00"),
        event(2, "2026-10-06T10:00:00"),
      ];

      expect(
        store.getEventsForDate({ date: "2026-10-05" }).map((e) => e.id)
      ).toEqual([1]);
    });
  });
});
