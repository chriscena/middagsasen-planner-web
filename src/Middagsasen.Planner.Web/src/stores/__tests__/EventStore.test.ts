import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  AcknowledgementReportResponse,
  AcknowledgementSummaryResponse,
  EventRequest,
  EventResponse,
  EventTemplateRequest,
  FacilityCompetencyWarningResponse,
  FileInfoResponse,
  FileMetadataRequest,
  MessageResponse,
  ResourceResponse,
  ResourceTypeRequest,
  ShiftResponse,
  ShiftResult,
  TrainingResponse,
  UserResponse,
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

// Instrukslisten hentes på nytt i bakgrunnen etter vakt- og
// vedleggsendringer; testene sjekker bare at det skjer.
const mockReloadInstructions = vi.hoisted(() => vi.fn());

vi.mock("@/stores/InstructionStore", () => ({
  reloadInstructionsInBackground: mockReloadInstructions,
}));

import { useAuthStore } from "@/stores/AuthStore";
import { useEventStore } from "@/stores/EventStore";

// AuthStore leser innlogget bruker fra localStorage når den ikke er satt i
// staten (f.eks. etter utlogging). Testmiljøet (node) har ingen.
vi.stubGlobal("localStorage", {
  getItem: () => null,
  setItem: () => {},
  removeItem: () => {},
});

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
    hasUnacknowledgedMandatoryInstructions: false,
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
    shiftCount: 2,
    shifts,
    messages: [],
    competencyWarnings: [],
    isMissingStaff: true,
    isFull: false,
    isPast: false,
    canSignUp: true,
    mustAnswerTraining: true,
    hasUnacknowledgedMandatoryInstructions: false,
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

function facilityWarning(
  competencyId: number,
  startTime: string
): FacilityCompetencyWarningResponse {
  return {
    competencyId,
    competencyName: "Førstehjelp",
    minimumRequired: 2,
    currentCount: 1,
    startTime,
    endTime: "2026-10-05T16:00",
  };
}

// Vaktoperasjonene henter vaktlisten på nytt (anleggskrav). Svarer på
// GET /api/events/{id} med vaktlisten slik den er i cachen etter
// operasjonen (JSON-kopi, ikke den reaktive proxyen).
function mockEventRefresh(store: { events: EventResponse[] }): void {
  mockApi.get.mockImplementation(async (url: string) => {
    const id = Number(url.replace("/api/events/", ""));
    const cached = store.events.find((e) => e.id === id);
    return { data: JSON.parse(JSON.stringify(cached ?? null)) };
  });
}

// Et svar fra api-mocken som testen selv bestemmer når kommer.
function deferred<T>() {
  let resolve!: (value: { data: T }) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<{ data: T }>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function logInAs(userId: number): void {
  useAuthStore().loggedInUser = { id: userId } as UserResponse;
}

describe("EventStore", () => {
  let store: ReturnType<typeof useEventStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useEventStore();
    // Nullstiller også mockResolvedValue o.l. fra forrige test.
    vi.resetAllMocks();
    vi.restoreAllMocks();
    logInAs(CURRENT_USER_ID);
  });

  describe("applyShiftResult", () => {
    it("oppdaterer oppgaven i cachen uten å bytte objektet", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      // Slik en dialog holder på oppgaven (selectedResource).
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

    it("henter vaktlisten på nytt uten changedTraining, så anleggskravene oppdateres", async () => {
      store.events = [
        {
          ...eventWith(1, [resource(10, [])]),
          competencyWarnings: [facilityWarning(3, "2026-10-05T10:00")],
        },
      ];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      const held = store.events[0]!.resources[0]!;
      const updated = resource(10, [shift(1, 10, CURRENT_USER_ID)]);
      mockApi.get.mockResolvedValueOnce({
        data: { ...eventWith(1, [updated]), competencyWarnings: [] },
      });

      await store.applyShiftResult(result(updated));

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(store.events[0]!.competencyWarnings).toEqual([]);
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.shifts.map((s) => s.id)).toEqual([1]);
    });

    it("henter perioden kalenderen viser på nytt ved changedTraining, uten å hente vaktlisten i tillegg", async () => {
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
      expect(mockApi.get).not.toHaveBeenCalledWith("/api/events/1");
      expect(store.events).toEqual(refetched);
    });

    it("henter vaktlisten ved changedTraining når ingen periode er hentet", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockEventRefresh(store);

      await store.applyShiftResult(
        result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
      );

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });

    it("feil ved ny henting av perioden kaster ikke, og oppgaven er oppdatert", async () => {
      const consoleError = vi
        .spyOn(console, "error")
        .mockImplementation(() => {});
      store.events = [eventWith(1, [resource(10, [])])];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      mockApi.get.mockRejectedValue(new Error("nettverk"));

      await expect(
        store.applyShiftResult(
          result(resource(10, [shift(1, 10, CURRENT_USER_ID)]), training(false))
        )
      ).resolves.toBeUndefined();

      expect(consoleError).toHaveBeenCalled();
      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });

    it("feil ved ny henting av vaktlisten kaster ikke, og oppgaven er oppdatert", async () => {
      const consoleError = vi
        .spyOn(console, "error")
        .mockImplementation(() => {});
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.get.mockRejectedValue(new Error("nettverk"));

      await expect(
        store.applyShiftResult(
          result(resource(10, [shift(1, 10, CURRENT_USER_ID)]))
        )
      ).resolves.toBeUndefined();

      expect(consoleError).toHaveBeenCalled();
      expect(store.events[0]!.resources[0]!.shifts.map((s) => s.id)).toEqual([
        1,
      ]);
    });
  });

  describe("saveShift", () => {
    beforeEach(() => mockEventRefresh(store));

    it("ledig vakt poster til oppgaven og legger svaret i cachen", async () => {
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
      // Vakta er tatt for en annen bruker: admins egen instruksliste er uendret.
      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("henter instrukslisten på nytt når innlogget bruker tar vakt selv", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.post.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });

      await store.saveShift({ resourceId: 10, shiftId: null, comment: null });

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("henter instrukslisten på nytt når admin tar vakt for seg selv med userId", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.post.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });

      await store.saveShift({
        resourceId: 10,
        shiftId: null,
        userId: CURRENT_USER_ID,
        comment: null,
      });

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("henter instrukslisten på nytt når en vakt endres, også til en annen bruker", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      mockApi.put.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, OTHER_USER_ID)])),
      });

      await store.saveShift({
        resourceId: 10,
        shiftId: 1,
        userId: OTHER_USER_ID,
        comment: null,
      });

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("henter ikke instrukslisten når brukeren logger ut før vakta er lagret", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const pending = deferred<ShiftResult>();
      mockApi.post.mockReturnValue(pending.promise);

      const request = store.saveShift({
        resourceId: 10,
        shiftId: null,
        comment: null,
      });
      useAuthStore().removeUserSession();
      pending.resolve({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });
      await request;

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("henter ikke instrukslisten når en annen bruker har logget inn før vakta er trukket", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      const pending = deferred<ShiftResult>();
      mockApi.delete.mockReturnValue(pending.promise);

      const request = store.withdraw(1);
      logInAs(OTHER_USER_ID);
      pending.resolve({ data: result(resource(10, [])) });
      await request;

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("henter ikke instrukslisten uten innlogget bruker", async () => {
      useAuthStore().removeUserSession();
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.post.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });

      await store.saveShift({ resourceId: 10, shiftId: null, comment: null });

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("henter ikke instrukslisten når vakta ikke ble lagret", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockApi.post.mockRejectedValue(new Error("Network Error"));

      await expect(
        store.saveShift({ resourceId: 10, shiftId: null, comment: null })
      ).rejects.toThrow("Network Error");

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("ledig vakt sender alltid comment, men utelater andre felt som ikke er satt", async () => {
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
    beforeEach(() => mockEventRefresh(store));

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

    it("withdraw sletter og legger oppgaven i cachen", async () => {
      store.events = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      const response = result(resource(10, []));
      mockApi.delete.mockResolvedValue({ data: response });

      const returned = await store.withdraw(1);

      // Svaret (med warnings) må nå komponenten, som viser dem til brukeren.
      expect(returned).toBe(response);
      expect(mockApi.delete).toHaveBeenCalledWith("/api/shifts/1");
      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(store.events[0]!.resources[0]!.shifts).toEqual([]);
      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });
  });

  describe("addEmptySlot", () => {
    it("poster uten body og legger oppgaven med flagg i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [shift(1, 10, CURRENT_USER_ID)], {
            shiftCount: 1,
            isMissingStaff: false,
            isFull: true,
          }),
        ]),
      ];
      const held = store.events[0]!.resources[0]!;
      const updated = resource(10, [shift(1, 10, CURRENT_USER_ID)], {
        shiftCount: 2,
        isMissingStaff: true,
        isFull: false,
      });
      mockApi.post.mockResolvedValue({ data: updated });

      const returned = await store.addEmptySlot(10);

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/emptySlots");
      expect(returned).toBe(updated);
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.shiftCount).toBe(2);
      expect(held.isMissingStaff).toBe(true);
      expect(held.isFull).toBe(false);
    });
  });

  describe("removeEmptySlot", () => {
    it("sletter uten body og legger oppgaven med flagg i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [shift(1, 10, CURRENT_USER_ID)], {
            shiftCount: 2,
            isMissingStaff: true,
            isFull: false,
          }),
        ]),
      ];
      const held = store.events[0]!.resources[0]!;
      const updated = resource(10, [shift(1, 10, CURRENT_USER_ID)], {
        shiftCount: 1,
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
      expect(held.shiftCount).toBe(1);
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

  // #145: endres events lokalt mens en henting pågår, kan svaret være eldre
  // enn endringen. Da henter getEventsForDates perioden på nytt.
  describe("lokale endringer under getEventsForDates", () => {
    const WEEK_URL = "/api/events?start=2026-10-05&end=2026-10-12";

    // Starter en henting av uke 41 som testen svarer på selv.
    function startFetch() {
      const response = deferred<EventResponse[]>();
      mockApi.get.mockReturnValueOnce(response.promise);
      const done = store.getEventsForDates("2026-10-05", "2026-10-11");
      return { response, done };
    }

    it("ny vaktliste fra mal under henting: henter på nytt og viser den", async () => {
      const fetch = startFetch();
      const created = event(2, "2026-10-07T10:00:00");
      mockApi.post.mockResolvedValue({ data: created });
      const second = [event(1, "2026-10-05T10:00:00"), created];
      mockApi.get.mockResolvedValueOnce({ data: second });

      await store.createEventFromTemplate(3, "2026-10-07");
      fetch.response.resolve({ data: [event(1, "2026-10-05T10:00:00")] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(mockApi.get).toHaveBeenLastCalledWith(WEEK_URL);
      expect(store.events).toEqual(second);
    });

    it("henter bare én gang uten lokale endringer", async () => {
      const fetch = startFetch();
      const server = [event(1, "2026-10-05T10:00:00")];

      fetch.response.resolve({ data: server });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(store.events).toEqual(server);
    });

    it("endringer uten pågående henting gir ikke ekstra henting senere", async () => {
      mockApi.post.mockResolvedValue({ data: event(2, "2026-10-07T10:00:00") });
      await store.createEventFromTemplate(3, "2026-10-07");
      const server = [event(1, "2026-10-05T10:00:00")];
      mockApi.get.mockResolvedValue({ data: server });

      await store.getEventsForDates("2026-10-05", "2026-10-11");

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(store.events).toEqual(server);
    });

    it("deleteEvent under henting gir ny henting", async () => {
      store.events = [event(1, "2026-10-05T10:00:00")];
      const fetch = startFetch();
      mockApi.delete.mockResolvedValue({});
      mockApi.get.mockResolvedValueOnce({ data: [] });

      await store.deleteEvent(1);
      fetch.response.resolve({ data: [event(1, "2026-10-05T10:00:00")] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(store.events).toEqual([]);
    });

    it("updateEvent under henting gir ny henting", async () => {
      store.events = [{ ...event(1, "2026-10-05T10:00:00"), name: "Før" }];
      const fetch = startFetch();
      const updated = { ...event(1, "2026-10-05T10:00:00"), name: "Etter" };
      mockApi.put.mockResolvedValue({ data: updated });
      mockApi.get.mockResolvedValueOnce({ data: [updated] });

      await store.updateEvent(1, {} as EventRequest);
      fetch.response.resolve({
        data: [{ ...event(1, "2026-10-05T10:00:00"), name: "Før" }],
      });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(store.events).toEqual([updated]);
    });

    it("updateEvent for en vaktliste som ikke er i cachen, gir også ny henting", async () => {
      const fetch = startFetch();
      const updated = event(1, "2026-10-05T10:00:00");
      mockApi.put.mockResolvedValue({ data: updated });
      mockApi.get.mockResolvedValueOnce({ data: [updated] });

      await store.updateEvent(1, {} as EventRequest);
      fetch.response.resolve({ data: [] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(store.events).toEqual([updated]);
    });

    it("saveShift (applyResource) under henting gir ny henting", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const fetch = startFetch();
      mockApi.post.mockResolvedValue({
        data: result(resource(10, [shift(1, 10, CURRENT_USER_ID)])),
      });
      const second = [
        eventWith(1, [resource(10, [shift(1, 10, CURRENT_USER_ID)])]),
      ];
      // Først vaktlisten (anleggskrav), så perioden på nytt.
      mockApi.get
        .mockResolvedValueOnce({ data: second[0] })
        .mockResolvedValueOnce({ data: second });

      await store.saveShift({ resourceId: 10, shiftId: null, comment: null });
      fetch.response.resolve({ data: [eventWith(1, [resource(10, [])])] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(3);
      expect(mockApi.get).toHaveBeenLastCalledWith(WEEK_URL);
      expect(store.events).toEqual(second);
    });

    it("ny beskjed under henting gir ny henting", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const fetch = startFetch();
      const message = { id: 5, message: "Hei" } as MessageResponse;
      mockApi.post.mockResolvedValue({ data: message });
      mockApi.get.mockResolvedValueOnce({
        data: [eventWith(1, [resource(10, [], { messages: [message] })])],
      });

      await store.addMessage(store.events[0]!.resources[0]!, {
        message: "Hei",
      });
      fetch.response.resolve({ data: [eventWith(1, [resource(10, [])])] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(store.events[0]!.resources[0]!.messages).toEqual([message]);
    });

    it("refreshEventResources under henting gir ny henting", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const fetch = startFetch();
      const fresh = eventWith(1, [resource(10, [], { shiftCount: 1 })]);
      mockApi.get
        .mockResolvedValueOnce({ data: fresh })
        .mockResolvedValueOnce({ data: [fresh] });

      await store.refreshEventResources(1);
      fetch.response.resolve({ data: [eventWith(1, [resource(10, [])])] });
      await fetch.done;

      expect(mockApi.get).toHaveBeenCalledTimes(3);
      expect(mockApi.get).toHaveBeenLastCalledWith(WEEK_URL);
      expect(store.events).toEqual([fresh]);
    });

    it("feil i den nye hentingen avviser getEventsForDates", async () => {
      const fetch = startFetch();
      mockApi.post.mockResolvedValue({ data: event(2, "2026-10-07T10:00:00") });
      const error = new Error("500");
      mockApi.get.mockRejectedValueOnce(error);

      await store.createEventFromTemplate(3, "2026-10-07");
      fetch.response.resolve({ data: [] });

      await expect(fetch.done).rejects.toBe(error);
    });

    it("henter høyst tre ganger og bruker siste svar ved konstante endringer", async () => {
      let call = 0;
      mockApi.get.mockImplementation(async () => {
        call++;
        store.markEventsChanged();
        return { data: [event(call, "2026-10-05T10:00:00")] };
      });

      await store.getEventsForDates("2026-10-05", "2026-10-11");

      expect(mockApi.get).toHaveBeenCalledTimes(3);
      expect(store.events.map((e) => e.id)).toEqual([3]);
    });

    it("to overlappende hentinger: den eldste forkastes uten ny henting", async () => {
      const old = deferred<EventResponse[]>();
      const latest = deferred<EventResponse[]>();
      const second = [event(2, "2026-10-07T10:00:00")];
      mockApi.get
        .mockReturnValueOnce(old.promise)
        .mockReturnValueOnce(latest.promise)
        .mockResolvedValueOnce({ data: second });
      const oldDone = store.getEventsForDates("2026-09-28", "2026-10-04");
      const latestDone = store.getEventsForDates("2026-10-05", "2026-10-11");

      store.markEventsChanged();
      latest.resolve({ data: [] });
      await latestDone;
      old.resolve({ data: [event(9, "2026-09-28T10:00:00")] });
      await oldDone;

      expect(mockApi.get).toHaveBeenCalledTimes(3);
      expect(store.events).toEqual(second);
    });

    it("createEventFromTemplate legger ikke inn duplikat når vaktlista allerede er hentet", async () => {
      const created = event(2, "2026-10-07T10:00:00");
      store.events = [event(1, "2026-10-05T10:00:00"), { ...created }];
      mockApi.post.mockResolvedValue({ data: created });

      await store.createEventFromTemplate(3, "2026-10-07");

      expect(store.events.map((e) => e.id)).toEqual([1, 2]);
      expect(store.events[1]).toEqual(created);
    });
  });

  describe("beskjeder", () => {
    it("addMessage legger beskjeden i oppgaven som sendes inn, også når den ikke er i events", async () => {
      const cached = resource(10, []);
      store.events = [eventWith(1, [cached])];
      // Som en dialog som holder på en oppgave fra før events ble hentet på nytt.
      const detached = resource(10, []);
      const message = { id: 5, message: "Hei" } as MessageResponse;
      mockApi.post.mockResolvedValue({ data: message });

      const returned = await store.addMessage(detached, { message: "Hei" });

      expect(mockApi.post).toHaveBeenCalledWith("/api/resources/10/messages", {
        message: "Hei",
      });
      expect(returned).toBe(message);
      expect(detached.messages).toEqual([message]);
      expect(store.events[0]!.resources[0]!.messages).toEqual([message]);
    });

    it("addMessage legger beskjeden inn én gang når oppgaven er den i events", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const held = store.events[0]!.resources[0]!;
      const message = { id: 5, message: "Hei" } as MessageResponse;
      mockApi.post.mockResolvedValue({ data: message });

      await store.addMessage(held, { message: "Hei" });

      expect(held.messages).toEqual([message]);
    });

    it("deleteMessage fjerner beskjeden fra oppgaven og fra events", async () => {
      const message = { id: 5, message: "Hei" } as MessageResponse;
      store.events = [
        eventWith(1, [resource(10, [], { messages: [message] })]),
      ];
      const detached = resource(10, [], { messages: [message] });
      mockApi.delete.mockResolvedValue({});

      await store.deleteMessage(detached, message);

      expect(mockApi.delete).toHaveBeenCalledWith(
        "/api/resources/10/messages/5"
      );
      expect(detached.messages).toEqual([]);
      expect(store.events[0]!.resources[0]!.messages).toEqual([]);
    });
  });

  describe("refreshEventResources", () => {
    it("henter arrangementet og legger oppgavene i cachen", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [], { shiftCount: 2, isMissingStaff: true }),
        ]),
        eventWith(2, [resource(20, [], { shiftCount: 3 })]),
      ];
      const held = store.events[0]!.resources[0]!;
      const other = store.events[1]!.resources[0]!;
      const fresh = eventWith(1, [
        resource(10, [shift(1, 10, OTHER_USER_ID)], {
          shiftCount: 1,
          isMissingStaff: false,
          isFull: true,
        }),
      ]);
      mockApi.get.mockResolvedValue({ data: fresh });

      await store.refreshEventResources(1);

      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(store.events[0]!.resources[0]).toBe(held);
      expect(held.shiftCount).toBe(1);
      expect(held.shifts).toHaveLength(1);
      expect(held.isMissingStaff).toBe(false);
      expect(held.isFull).toBe(true);
      expect(other.shiftCount).toBe(3);
    });

    it("synkroniserer oppgavelisten: oppdaterer, fjerner slettede og legger til nye i serverens rekkefølge", async () => {
      store.events = [
        eventWith(1, [
          resource(10, [], { shiftCount: 2 }),
          resource(11, [], { shiftCount: 4 }),
        ]),
        eventWith(2, [resource(20, [], { shiftCount: 3 })]),
      ];
      const cachedEvent = store.events[0]!;
      const kept = cachedEvent.resources[0]!;
      const otherEvent = store.events[1]!;
      const otherResources = otherEvent.resources;
      const added = resource(12, [], { shiftCount: 5 });
      const fresh = {
        ...eventWith(1, [added, resource(10, [], { shiftCount: 1 })]),
        name: "Nytt navn",
      } as EventResponse;
      mockApi.get.mockResolvedValue({ data: fresh });

      await store.refreshEventResources(1);

      expect(store.events[0]).toBe(cachedEvent);
      expect(cachedEvent.name).toBe("Nytt navn");
      expect(cachedEvent.resources.map((r) => r.id)).toEqual([12, 10]);
      expect(cachedEvent.resources[1]).toBe(kept);
      expect(kept.shiftCount).toBe(1);
      expect(cachedEvent.resources[0]).toEqual(added);
      expect(store.events[1]).toBe(otherEvent);
      expect(otherEvent.resources).toBe(otherResources);
      expect(otherResources.map((r) => r.shiftCount)).toEqual([3]);
    });

    it("fjerner arrangementet fra cachen når det ikke finnes lenger (404)", async () => {
      store.events = [
        eventWith(1, [resource(10, [])]),
        eventWith(2, [resource(20, [])]),
      ];
      const other = store.events[1]!;
      mockApi.get.mockRejectedValue({ response: { status: 404 } });

      await store.refreshEventResources(1);

      expect(store.events).toEqual([other]);
      expect(store.events[0]).toBe(other);
    });

    it("gjør ingenting når arrangementet ikke er i cachen", async () => {
      store.events = [eventWith(2, [resource(20, [])])];
      mockApi.get.mockResolvedValue({
        data: eventWith(1, [resource(10, [])]),
      });

      await store.refreshEventResources(1);

      expect(store.events.map((e) => e.id)).toEqual([2]);
    });

    it("kaster videre når hentingen feiler", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      const error = Object.assign(new Error("500"), {
        response: { status: 500 },
      });
      mockApi.get.mockRejectedValue(error);

      await expect(store.refreshEventResources(1)).rejects.toBe(error);
      expect(store.events.map((e) => e.id)).toEqual([1]);
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

    it("henter arrangementet og setter selectedEvent", async () => {
      const fresh = event(2, "2026-10-05T10:00:00");
      mockApi.get.mockResolvedValue({ data: fresh });

      await store.getEvent(2);

      expect(mockApi.get).toHaveBeenCalledWith("/api/events/2");
      expect(store.selectedEvent).toEqual(fresh);
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

  describe("createTemplate", () => {
    const template: EventTemplateRequest = {
      name: "Disko",
      eventName: "Diskokveld",
      startTime: "18:00",
      endTime: "21:00",
      resourceTemplates: [],
      competencyRequirements: [{ competencyId: 3, minimumRequired: 1 }],
    };

    it("sender anleggskravene med", async () => {
      mockApi.post.mockResolvedValue({ data: {} });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.createTemplate(template);

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/templates",
        expect.objectContaining({
          competencyRequirements: [{ competencyId: 3, minimumRequired: 1 }],
        })
      );
    });

    it("sender tom liste når malen ikke har anleggskrav", async () => {
      mockApi.post.mockResolvedValue({ data: {} });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.createTemplate({ ...template, competencyRequirements: [] });

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/templates",
        expect.objectContaining({ competencyRequirements: [] })
      );
    });
  });

  describe("vedlegg på vakttype", () => {
    const fileInfo: FileInfoResponse = {
      id: 7,
      resourceTypeId: TYPE_ID,
      fileName: "instruks.pdf",
      description: "Heisførerinstruks",
      created: "2026-10-01T10:00:00Z",
      createdBy: "Admin",
      updated: "2026-10-01T10:00:00Z",
      updatedBy: "Admin",
      mimeType: "application/pdf",
      requiresAcknowledgement: true,
      isMandatory: true,
    };

    it("addResourceTypeFile sender fil, beskrivelse og flagg som multipart og henter vakttypene på nytt", async () => {
      const blob = new Blob(["pdf"], { type: "application/pdf" });
      mockApi.post.mockResolvedValue({ data: fileInfo });
      mockApi.get.mockResolvedValue({ data: [] });

      const result = await store.addResourceTypeFile(
        { id: TYPE_ID },
        {
          file: blob,
          description: "Heisførerinstruks",
          requiresAcknowledgement: true,
          isMandatory: true,
        }
      );

      expect(mockApi.post).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files`,
        expect.any(FormData)
      );
      const formData = mockApi.post.mock.calls[0]?.[1] as FormData;
      // FormData pakker Blob inn som File, så innholdet sammenlignes.
      const sent = formData.get("file") as Blob;
      expect(sent).toBeInstanceOf(Blob);
      expect(sent.type).toBe("application/pdf");
      expect(await sent.text()).toBe("pdf");
      expect(formData.get("description")).toBe("Heisførerinstruks");
      expect(formData.get("requiresAcknowledgement")).toBe("true");
      expect(formData.get("isMandatory")).toBe("true");
      expect(mockApi.get).toHaveBeenCalledWith("/api/resourcetypes");
      expect(result).toEqual(fileInfo);
      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("addResourceTypeFile sender false-flagg eksplisitt", async () => {
      mockApi.post.mockResolvedValue({ data: fileInfo });
      mockApi.get.mockResolvedValue({ data: [] });

      await store.addResourceTypeFile(
        { id: TYPE_ID },
        {
          file: new Blob(["x"]),
          description: "Løypekart",
          requiresAcknowledgement: false,
          isMandatory: false,
        }
      );

      const formData = mockApi.post.mock.calls[0]?.[1] as FormData;
      expect(formData.get("requiresAcknowledgement")).toBe("false");
      expect(formData.get("isMandatory")).toBe("false");
    });

    it("updateResourceTypeFile sender PUT med metadata og henter vakttypene på nytt", async () => {
      const request: FileMetadataRequest = {
        description: "Ny beskrivelse",
        requiresAcknowledgement: true,
        isMandatory: false,
      };
      const updated = { ...fileInfo, ...request };
      mockApi.put.mockResolvedValue({ data: updated });
      mockApi.get.mockResolvedValue({ data: [] });

      const result = await store.updateResourceTypeFile(TYPE_ID, 7, request);

      expect(mockApi.put).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/7`,
        request
      );
      expect(mockApi.get).toHaveBeenCalledWith("/api/resourcetypes");
      expect(result).toEqual(updated);
      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("deleteResourceTypeFile arkiverer med DELETE og henter vakttypene på nytt", async () => {
      mockApi.delete.mockResolvedValue({});
      mockApi.get.mockResolvedValue({ data: [] });

      await store.deleteResourceTypeFile(fileInfo);

      expect(mockApi.delete).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/7`
      );
      expect(mockApi.get).toHaveBeenCalledWith("/api/resourcetypes");
      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });
  });

  describe("instrukslisten hentes på nytt", () => {
    it("etter deleteEvent", async () => {
      mockApi.delete.mockResolvedValue({});

      await store.deleteEvent(1);

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("etter updateEvent", async () => {
      mockApi.put.mockResolvedValue({ data: eventWith(1, []) });

      await store.updateEvent(1, {} as EventRequest);

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("etter updateResourceType", async () => {
      mockApi.put.mockResolvedValue({});
      mockApi.get.mockResolvedValue({ data: [] });

      await store.updateResourceType({ id: TYPE_ID } as ResourceTypeRequest & {
        id: number;
      });

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("etter deleteResourceType", async () => {
      mockApi.delete.mockResolvedValue({});
      mockApi.get.mockResolvedValue({ data: [] });

      await store.deleteResourceType({ id: TYPE_ID });

      expect(mockReloadInstructions).toHaveBeenCalledTimes(1);
    });

    it("ikke når kallet feiler", async () => {
      mockApi.delete.mockRejectedValue(new Error("Network Error"));

      await expect(store.deleteEvent(1)).rejects.toThrow("Network Error");

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });

    it("ikke etter createResourceType eller setTraining", async () => {
      store.events = [eventWith(1, [resource(10, [])])];
      mockEventRefresh(store);
      mockApi.post.mockResolvedValue({ data: { id: TYPE_ID } });
      mockApi.put.mockResolvedValue({
        data: result(resource(10, [shift(7, 10, CURRENT_USER_ID)])),
      });

      await store.createResourceType({} as ResourceTypeRequest);
      await store.setTraining(7, true);

      expect(mockReloadInstructions).not.toHaveBeenCalled();
    });
  });

  describe("kvitteringsrapport og arkiv", () => {
    const archived: FileInfoResponse = {
      id: 8,
      resourceTypeId: TYPE_ID,
      fileName: "gammel-instruks.pdf",
      description: "Gammel instruks",
      created: "2025-10-01T10:00:00Z",
      createdBy: "Admin",
      updated: "2025-10-01T10:00:00Z",
      updatedBy: "Admin",
      mimeType: "application/pdf",
      requiresAcknowledgement: true,
      isMandatory: false,
      archived: "2026-09-01T10:00:00Z",
    };
    const report: AcknowledgementReportResponse = {
      file: archived,
      total: 2,
      acknowledgedCount: 1,
      users: [
        {
          userId: 3,
          fullName: "Kari Nordmann",
          acknowledgedAt: null,
          comment: null,
          appliesTo: true,
        },
        {
          userId: 4,
          fullName: "Ola Nordmann",
          acknowledgedAt: "2026-09-15T08:30:00Z",
          comment: "Lest",
          appliesTo: true,
        },
      ],
    };

    it("getAcknowledgementReport sender all=false som standard og returnerer rapporten", async () => {
      mockApi.get.mockResolvedValue({ data: report });

      const result = await store.getAcknowledgementReport(TYPE_ID, 8);

      expect(mockApi.get).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/8/acknowledgements`,
        { params: { all: false } }
      );
      expect(result).toEqual(report);
    });

    it("getAcknowledgementReport sender all=true når alle brukere skal med", async () => {
      mockApi.get.mockResolvedValue({ data: report });

      await store.getAcknowledgementReport(TYPE_ID, 8, true);

      expect(mockApi.get).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/8/acknowledgements`,
        { params: { all: true } }
      );
    });

    it("getAcknowledgementReport kaster feilen videre", async () => {
      const error = new Error("403");
      mockApi.get.mockRejectedValue(error);

      await expect(store.getAcknowledgementReport(TYPE_ID, 8)).rejects.toBe(
        error
      );
    });

    it("getAcknowledgementSummaries henter tellerne for vakttypen i ett kall", async () => {
      const summaries: AcknowledgementSummaryResponse[] = [
        { fileId: 7, acknowledgedCount: 12, total: 18 },
        { fileId: 9, acknowledgedCount: 0, total: 18 },
      ];
      mockApi.get.mockResolvedValue({ data: summaries });

      const result = await store.getAcknowledgementSummaries(TYPE_ID);

      expect(mockApi.get).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/acknowledgements`
      );
      expect(result).toEqual(summaries);
    });

    it("getAcknowledgementSummaries kaster feilen videre", async () => {
      const error = new Error("403");
      mockApi.get.mockRejectedValue(error);

      await expect(store.getAcknowledgementSummaries(TYPE_ID)).rejects.toBe(
        error
      );
    });

    it("getArchivedResourceTypeFiles henter arkiverte vedlegg for vakttypen", async () => {
      mockApi.get.mockResolvedValue({ data: [archived] });

      const result = await store.getArchivedResourceTypeFiles(TYPE_ID);

      expect(mockApi.get).toHaveBeenCalledWith(
        `/api/resourcetypes/${TYPE_ID}/files/archived`
      );
      expect(result).toEqual([archived]);
    });

    it("getArchivedResourceTypeFiles kaster feilen videre", async () => {
      const error = new Error("500");
      mockApi.get.mockRejectedValue(error);

      await expect(store.getArchivedResourceTypeFiles(TYPE_ID)).rejects.toBe(
        error
      );
    });
  });

  describe("refreshAfterAcknowledgement", () => {
    it("henter perioden kalenderen viser i ett kall og kopierer den ferske oppgaven inn i dialogens objekt", async () => {
      const cached = resource(10, [], {
        hasUnacknowledgedMandatoryInstructions: true,
      });
      store.events = [{ ...event(1, "2026-10-05T10:00"), resources: [cached] }];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };

      const fresh = resource(10, [], {
        hasUnacknowledgedMandatoryInstructions: false,
      });
      mockApi.get.mockResolvedValue({
        data: [{ ...event(1, "2026-10-05T10:00"), resources: [fresh] }],
      });

      await store.refreshAfterAcknowledgement(cached);

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(mockApi.get).toHaveBeenCalledWith(
        expect.stringContaining("/api/events?start=2026-10-05")
      );
      // events er byttet ut (nye objekter), men dialogens objekt har de nye
      // verdiene.
      expect(cached.hasUnacknowledgedMandatoryInstructions).toBe(false);
      expect(store.events[0]?.resources[0]).toEqual(fresh);
      expect(store.events[0]?.resources[0]).not.toBe(cached);
    });

    it("henter bare vaktlisten når kalenderen ikke har hentet noen periode", async () => {
      const cached = resource(10, [], {
        hasUnacknowledgedMandatoryInstructions: true,
      });
      store.events = [{ ...event(1, "2026-10-05T10:00"), resources: [cached] }];
      store.eventsRange = null;
      mockApi.get.mockResolvedValue({
        data: {
          ...event(1, "2026-10-05T10:00"),
          resources: [
            resource(10, [], { hasUnacknowledgedMandatoryInstructions: false }),
          ],
        },
      });

      await store.refreshAfterAcknowledgement(cached);

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(mockApi.get).toHaveBeenCalledWith("/api/events/1");
      expect(cached.hasUnacknowledgedMandatoryInstructions).toBe(false);
    });

    it("lar dialogens objekt være når oppgaven ikke finnes i svaret", async () => {
      const cached = resource(10, [], {
        hasUnacknowledgedMandatoryInstructions: true,
      });
      store.events = [{ ...event(1, "2026-10-05T10:00"), resources: [cached] }];
      store.eventsRange = { start: "2026-10-05", end: "2026-10-11" };
      mockApi.get.mockResolvedValue({ data: [] });

      await store.refreshAfterAcknowledgement(cached);

      expect(cached.hasUnacknowledgedMandatoryInstructions).toBe(true);
    });
  });
});
