import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  EventResponse,
  ResourceResponse,
  ShiftResponse,
  ShiftResult,
  TrainingResponse,
  UserResponse,
} from "src/types";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
}));

vi.mock("boot/axios", () => ({
  api: mockApi,
}));

import { useEventStore } from "stores/EventStore";
import { useAuthStore } from "stores/AuthStore";

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
    startTime: "2026-10-05T10:00:00",
    endTime: "2026-10-05T16:00:00",
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
  let authStore: ReturnType<typeof useAuthStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useEventStore();
    authStore = useAuthStore();
    vi.clearAllMocks();
    // setUser lagrer brukeren i localStorage, som ikke finnes i node.
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
    });
    authStore.loggedInUser = {
      id: CURRENT_USER_ID,
      phoneNo: "1",
      isAdmin: false,
      isHidden: false,
      trainings: [],
    } satisfies UserResponse;
  });

  describe("applyShiftResult", () => {
    it("oppdaterer ressursen i cachen uten å bytte objektet", () => {
      store.events = [eventWith(1, [resource(10, [])])];
      // Slik en dialog holder på ressursen (selectedResource).
      const held = store.events[0]!.resources[0]!;

      store.applyShiftResult(
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

    it("lar andre ressurser være når changedTraining mangler", () => {
      store.events = [
        eventWith(1, [
          resource(10, []),
          resource(11, [shift(2, 11, CURRENT_USER_ID)]),
        ]),
      ];

      store.applyShiftResult(result(resource(10, [])));

      expect(store.events[0]!.resources[1]!.mustAnswerTraining).toBe(true);
    });

    it("oppdaterer ressurser av samme type når innlogget bruker ønsker opplæring", () => {
      const otherType = resource(12, [shift(3, 12, CURRENT_USER_ID)], {
        resourceType: { id: 99 } as ResourceResponse["resourceType"],
      });
      store.events = [
        eventWith(1, [resource(10, [])]),
        eventWith(2, [
          resource(11, [shift(2, 11, CURRENT_USER_ID)]),
          otherType,
        ]),
      ];

      store.applyShiftResult(
        result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
      );

      const [sameType, untouched] = store.events[1]!.resources;
      expect(sameType!.mustAnswerTraining).toBe(false);
      expect(sameType!.shifts[0]!.needsTraining).toBe(true);
      expect(sameType!.shifts[0]!.user.trainings).toEqual([training(false)]);
      expect(untouched!.mustAnswerTraining).toBe(true);
      expect(untouched!.shifts[0]!.needsTraining).toBe(false);
      expect(authStore.loggedInUser!.trainings).toEqual([training(false)]);
    });

    it("bekreftet opplæring gjelder bare vaktene til eieren", () => {
      store.events = [
        eventWith(1, [
          resource(10, []),
          resource(
            11,
            [
              shift(2, 11, OTHER_USER_ID, {
                needsTraining: true,
                canConfirmTraining: true,
                user: {
                  id: OTHER_USER_ID,
                  phoneNumber: "2",
                  trainings: [training(false, OTHER_USER_ID)],
                },
              }),
              shift(3, 11, 3, {
                needsTraining: true,
                canConfirmTraining: true,
              }),
            ],
            { mustAnswerTraining: false }
          ),
        ]),
      ];

      store.applyShiftResult(
        result(
          resource(10, [shift(1, 10, OTHER_USER_ID)]),
          training(true, OTHER_USER_ID)
        )
      );

      const other = store.events[0]!.resources[1]!;
      const [ownerShift, otherUserShift] = other.shifts;
      expect(ownerShift!.needsTraining).toBe(false);
      expect(ownerShift!.canConfirmTraining).toBe(false);
      expect(ownerShift!.user.trainings).toEqual([
        training(true, OTHER_USER_ID),
      ]);
      expect(otherUserShift!.needsTraining).toBe(true);
      expect(otherUserShift!.canConfirmTraining).toBe(true);
      // Gjelder ikke innlogget bruker.
      expect(other.mustAnswerTraining).toBe(false);
      expect(authStore.loggedInUser!.trainings).toEqual([]);
    });
  });

  describe("vaktoperasjoner", () => {
    it("signUp poster requesten og bruker changedTraining.userId for opplæringen", async () => {
      store.events = [eventWith(1, [resource(10, []), resource(11, [])])];
      const response = result(
        resource(10, [shift(1, 10, CURRENT_USER_ID)]),
        training(true)
      );
      mockApi.post.mockResolvedValue({ data: response });

      const returned = await store.signUp(10, { needsTraining: false });

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/shifts", {
        needsTraining: false,
      });
      expect(returned).toBe(response);
      expect(store.events[0]!.resources[1]!.mustAnswerTraining).toBe(false);
    });

    it("signUp for en annen bruker oppdaterer ikke innlogget bruker", async () => {
      store.events = [eventWith(1, [resource(10, []), resource(11, [])])];
      mockApi.post.mockResolvedValue({
        data: result(
          resource(10, [shift(1, 10, OTHER_USER_ID)]),
          training(false, OTHER_USER_ID)
        ),
      });

      await store.signUp(10, { userId: OTHER_USER_ID, needsTraining: true });

      expect(store.events[0]!.resources[1]!.mustAnswerTraining).toBe(true);
      expect(authStore.loggedInUser!.trainings).toEqual([]);
    });

    it("setTraining bruker changedTraining.userId", async () => {
      store.events = [
        eventWith(1, [
          resource(10, []),
          resource(11, [shift(2, 11, OTHER_USER_ID, { needsTraining: true })]),
        ]),
      ];
      mockApi.put.mockResolvedValue({
        data: result(
          resource(10, [shift(7, 10, OTHER_USER_ID)]),
          training(true, OTHER_USER_ID)
        ),
      });

      await store.setTraining(7, true);

      expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/7/training", {
        trainingCompleted: true,
      });
      expect(store.events[0]!.resources[1]!.shifts[0]!.needsTraining).toBe(
        false
      );
    });

    it("changeShift sender requesten til vakta", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      const updated = shift(1, 10, CURRENT_USER_ID, { comment: "Hei" });
      mockApi.put.mockResolvedValue({ data: result(resource(10, [updated])) });

      await store.changeShift(1, { comment: "Hei" });

      expect(mockApi.put).toHaveBeenCalledWith("/api/shifts/1", {
        comment: "Hei",
      });
      expect(store.events[0]!.resources[0]!.shifts[0]!.comment).toBe("Hei");
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

  describe("patchMinimumStaff", () => {
    it("sender ny verdi og legger ressursen med flagg i cachen", async () => {
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
      mockApi.patch.mockResolvedValue({ data: updated });

      const returned = await store.patchMinimumStaff(10, 1);

      expect(mockApi.patch).toHaveBeenCalledWith(
        "/api/resources/10/minimumStaff",
        { minimumStaff: 1 }
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
