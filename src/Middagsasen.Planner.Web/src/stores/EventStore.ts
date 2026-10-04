import { defineStore } from "pinia";
import { api } from "@/boot/axios";
import { getErrorResponse } from "@/shared/apiError";
import { nextDay, toDayKey } from "@/shared/time";
import type {
  ChangeShiftRequest,
  EventFromTemplateRequest,
  EventRequest,
  EventResponse,
  EventStatusResponse,
  EventTemplateRequest,
  EventTemplateResponse,
  FileInfoResponse,
  MessageRequest,
  MessageResponse,
  ResourceResponse,
  ResourceTypeRequest,
  ResourceTypeResponse,
  SetTrainingRequest,
  ShiftResult,
  SignUpRequest,
  TemplateFromEventRequest,
} from "@/types";

interface EventState {
  selectedEvent: EventResponse | null;
  events: EventResponse[];
  resourceTypes: ResourceTypeResponse[];
  templates: EventTemplateResponse[];
  // Dato (yyyy/MM/dd, se parseEventStatusDate) -> om vaktlistene den dagen
  // mangler mannskap.
  eventStatuses: Record<string, boolean>;
  // Siste periode hentet med getEventsForDates (det kalenderen viser), så
  // events kan hentes på nytt etter en endring som påvirker flere ressurser.
  eventsRange: { start: string; end: string } | null;
}

// Lagre en vakt med saveShift. shiftId null = ta ledig plass på resourceId
// (POST), ellers endre vakta (PUT). userId utelatt/null = innlogget bruker
// (annen bruker kun admin). comment er påkrevd og sendes alltid: backend
// setter kommentaren ved endring, og manglende felt tolkes som null og sletter
// den lagrede kommentaren. trainingCompleted har samme betydning som
// TrainingComplete: true = gjennomført eller trengs ikke, false = ønsker
// opplæring, null/utelatt = ikke svart (backend avgjør om svaret er påkrevd).
export interface SaveShiftRequest {
  resourceId: number;
  shiftId: number | null;
  userId?: number | null;
  comment: string | null;
  trainingCompleted?: boolean | null;
}

// Løpenummer for getEventsForDates, slik at et tregt svar på en eldre
// forespørsel ikke overskriver events fra en nyere (f.eks. rask bla i uker).
let latestEventsRequest = 0;

// Lokale endringer i events (ny, endret eller slettet vaktliste, eller en
// ressurs fra en vaktoperasjon) som skjer mens den nyeste getEventsForDates
// er underveis. Svaret fra serveren kan være eldre enn endringen, så de
// spilles av på svaret i stedet for å gå tapt (#145). Bare den nyeste
// forespørselen skriver events, så endringer logges kun mens den pågår, og
// loggen tømmes når den er ferdig.
type LocalChange =
  | { kind: "upsert"; event: EventResponse }
  | { kind: "remove"; id: number }
  | { kind: "resource"; resource: ResourceResponse };
let latestLocalChange = 0;
let localChanges: { seq: number; change: LocalChange }[] = [];
let latestEventsRequestPending = false;

function recordLocalChange(change: LocalChange): void {
  if (!latestEventsRequestPending) return;
  localChanges.push({ seq: ++latestLocalChange, change });
}

// Erstatter ressursen med samme id (i alle events) med Object.assign, så
// objektet beholder identiteten (se applyResource).
function assignResource(
  events: EventResponse[],
  updated: ResourceResponse
): void {
  for (const event of events) {
    const resource = event.resources.find((r) => r.id === updated.id);
    if (resource) Object.assign(resource, updated);
  }
}

// Spiller av endringene etter afterSeq på events fra serveren. En vaktliste
// som ikke finnes i svaret, legges bare til hvis den starter innenfor
// perioden som ble hentet (firstDay til og med lastDay, som dagnøkler).
function replayLocalChanges(
  events: EventResponse[],
  afterSeq: number,
  firstDay: string,
  lastDay: string
): EventResponse[] {
  let result = events;
  for (const { seq, change } of localChanges) {
    if (seq <= afterSeq) continue;
    if (change.kind === "remove") {
      result = result.filter((e) => e.id !== change.id);
    } else if (change.kind === "resource") {
      assignResource(result, change.resource);
    } else if (result.some((e) => e.id === change.event.id)) {
      result = result.map((e) => (e.id === change.event.id ? change.event : e));
    } else {
      const day = toDayKey(change.event.startTime);
      if (day >= firstDay && day <= lastDay) result = [...result, change.event];
    }
  }
  return result;
}

// Felles henting av ett arrangement (getEvent og refreshEventResources).
async function fetchEvent(id: number | string): Promise<EventResponse> {
  const response = await api.get<EventResponse>(`/api/events/${id}`);
  return response.data;
}

export const useEventStore = defineStore("events", {
  state: (): EventState => ({
    selectedEvent: null,
    events: [],
    resourceTypes: [],
    templates: [],
    eventStatuses: {},
    eventsRange: null,
  }),
  getters: {
    getEventsForDate:
      (state) =>
      (timestamp: { date: string }): EventResponse[] => {
        return state.events.filter((e) => {
          return e.startTime.startsWith(timestamp.date);
        });
      },
    eventStatusDates: (state): string[] => [
      ...Object.keys(state.eventStatuses),
    ],
  },
  actions: {
    async getEventStatuses(month: number, year: number): Promise<void> {
      const response = await api.get<EventStatusResponse[]>(
        `/api/eventstatus?month=${month}&year=${year}`
      );
      const statuses = response.data;
      for (let index = 0; index < statuses.length; index++) {
        const status = statuses[index]!;
        this.eventStatuses[status.date] = status.isMissingStaff;
      }
    },
    async getEventsForDates(start: string, end: string): Promise<void> {
      const firstDay = toDayKey(start);
      const lastDay = toDayKey(end);
      const startDate = encodeURIComponent(firstDay);
      const endDate = encodeURIComponent(toDayKey(nextDay(end)));
      this.eventsRange = { start, end };
      const request = ++latestEventsRequest;
      const changesBefore = latestLocalChange;
      latestEventsRequestPending = true;
      try {
        const response = await api.get<EventResponse[]>(
          `/api/events?start=${startDate}&end=${endDate}`
        );
        if (request !== latestEventsRequest) return;
        this.events = replayLocalChanges(
          response.data,
          changesBefore,
          firstDay,
          lastDay
        );
      } finally {
        // Eldre forespørsler forkastes, så når den nyeste er ferdig (med
        // eller uten feil), trengs ikke loggen lenger.
        if (request === latestEventsRequest) {
          latestEventsRequestPending = false;
          localChanges = [];
        }
      }
    },
    async addEvent(event: EventRequest): Promise<void> {
      const response = await api.post<EventResponse>("/api/events", event);
      this.events.push(response.data);
      recordLocalChange({ kind: "upsert", event: response.data });
    },
    // id kan være en streng når den kommer fra en route-param (EventPage).
    async getEvent(id: number | string): Promise<void> {
      // Nullstilles først, så en mislykket lasting aldri etterlater en
      // tidligere lastet vaktliste (som Slett ellers kunne slettet).
      this.selectedEvent = null;
      this.selectedEvent = await fetchEvent(id);
    },
    async deleteEvent(id: number): Promise<void> {
      await api.delete(`/api/events/${id}`);
      this.selectedEvent = null;
      this.events = this.events.filter((e) => e.id !== id);
      recordLocalChange({ kind: "remove", id });
    },
    async updateEvent(id: number | string, event: EventRequest): Promise<void> {
      const response = await api.put<EventResponse>(`/api/events/${id}`, event);
      const updatedEvent = response.data;
      // findIndex i stedet for indexOf(find(...)): samme resultat (-1 når den
      // ikke finnes), men typesikkert.
      const replaceIndex = this.events.findIndex(
        (event) => event.id === updatedEvent.id
      );
      if (replaceIndex > -1) {
        this.events[replaceIndex] = updatedEvent;
        recordLocalChange({ kind: "upsert", event: updatedEvent });
      }
    },

    async createResourceType(
      resourceType: ResourceTypeRequest
    ): Promise<ResourceTypeResponse> {
      const response = await api.post<ResourceTypeResponse>(
        "/api/resourcetypes",
        resourceType
      );
      await this.getResourceTypes();
      return response.data;
    },

    // ResourceTypesPage sender hele det redigerte objektet (ResourceTypeResponse
    // med evt. nye trenere); id brukes i URL-en.
    async updateResourceType(
      resourceType: ResourceTypeRequest & { id: number }
    ): Promise<void> {
      await api.put(`/api/resourcetypes/${resourceType.id}`, resourceType);
      await this.getResourceTypes();
    },

    async deleteResourceType(
      resourceType: Pick<ResourceTypeResponse, "id">
    ): Promise<void> {
      await api.delete(`/api/resourcetypes/${resourceType.id}`);
      await this.getResourceTypes();
    },
    async getResourceTypes(): Promise<void> {
      // if (this.resourceTypes.length) return;
      const response =
        await api.get<ResourceTypeResponse[]>("/api/resourcetypes");
      this.resourceTypes = response.data;
    },
    // --- Vakter ---
    // Alle skriveoperasjonene returnerer ShiftResult med hele ressursen etter
    // endringen (med flagg for innlogget bruker). Svaret legges i cachen med
    // applyShiftResult og returneres, så komponenten kan vise warnings.

    // Ta ledig plass eller endre vakta i ett kall (én transaksjon i backend).
    // shiftId null = POST på ressursen, ellers PUT på vakta. Felt som ikke er
    // satt, sendes ikke (exactOptionalPropertyTypes). comment sendes alltid,
    // ved både POST og PUT.
    async saveShift(request: SaveShiftRequest): Promise<ShiftResult> {
      const { resourceId, shiftId, userId, comment, trainingCompleted } =
        request;
      const optional = {
        ...(userId !== undefined ? { userId } : {}),
        ...(trainingCompleted !== undefined ? { trainingCompleted } : {}),
      };
      const response =
        shiftId === null
          ? await api.post<ShiftResult>(`/api/resources/${resourceId}/shifts`, {
              ...optional,
              comment,
            } satisfies SignUpRequest)
          : await api.put<ShiftResult>(`/api/shifts/${shiftId}`, {
              ...optional,
              comment,
            } satisfies ChangeShiftRequest);
      const result = response.data;
      await this.applyShiftResult(result);
      return result;
    },
    // Sette opplæringen til eieren av vakta på ressursens ressurstype.
    async setTraining(
      shiftId: number,
      trainingCompleted: boolean
    ): Promise<ShiftResult> {
      const response = await api.put<ShiftResult>(
        `/api/shifts/${shiftId}/training`,
        { trainingCompleted } satisfies SetTrainingRequest
      );
      const result = response.data;
      await this.applyShiftResult(result);
      return result;
    },
    // Trekke seg fra / slette vakta.
    async withdraw(shiftId: number): Promise<ShiftResult> {
      const response = await api.delete<ShiftResult>(`/api/shifts/${shiftId}`);
      const result = response.data;
      await this.applyShiftResult(result);
      return result;
    },
    // Erstatter ressursen med samme id (i alle events) med svaret fra serveren.
    // Object.assign, så objektet beholder identiteten: komponenter og dialoger
    // som holder på ressursen (f.eks. selectedResource) ser de nye verdiene.
    // Logges som en egen ressursendring (ikke upsert av hele arrangementet),
    // så resten av arrangementet i svaret fra en pågående henting beholdes.
    applyResource(updated: ResourceResponse): void {
      assignResource(this.events, updated);
      recordLocalChange({ kind: "resource", resource: updated });
    },
    // Henter arrangementet på nytt og synkroniserer det i events-cachen med
    // svaret: ressurslisten får serverens innhold og rekkefølge, slettede
    // ressurser fjernes og nye legges til. Eksisterende ressurser (og
    // arrangementet selv) oppdateres med Object.assign, så objektene beholder
    // identiteten (som i applyResource). Brukes for å rette opp utdaterte tall
    // etter at en operasjon er avvist fordi noen andre har endret ressursen
    // (eller slettet den/arrangementet) i mellomtiden. Finnes ikke
    // arrangementet lenger (404), fjernes det fra cachen. selectedEvent røres
    // ikke: den er skjemadata for redigering og hentes på nytt av EventPage.
    async refreshEventResources(eventId: number): Promise<void> {
      let fresh: EventResponse;
      try {
        fresh = await fetchEvent(eventId);
      } catch (error) {
        if (getErrorResponse(error)?.status === 404) {
          this.events = this.events.filter((e) => e.id !== eventId);
          recordLocalChange({ kind: "remove", id: eventId });
          return;
        }
        throw error;
      }
      const event = this.events.find((e) => e.id === eventId);
      if (!event) return;
      const existing = new Map(event.resources.map((r) => [r.id, r]));
      const resources = fresh.resources.map((r) => {
        const cached = existing.get(r.id);
        return cached ? Object.assign(cached, r) : r;
      });
      Object.assign(event, fresh, { resources });
      recordLocalChange({ kind: "upsert", event });
    },
    // Legger svaret fra en vaktoperasjon i cachen via applyResource. Ble en
    // opplæring endret (changedTraining), kan flaggene på andre ressurser av
    // samme ressurstype (mustAnswerTraining, needsTraining, canConfirmTraining
    // osv.) også være endret. De beregnes av serveren, så perioden kalenderen
    // viser hentes på nytt. Operasjonen har lyktes uansett, så feil i
    // hentingen ignoreres (cachen er da bare ikke oppdatert for de andre).
    async applyShiftResult(result: ShiftResult): Promise<void> {
      this.applyResource(result.resource);
      if (!result.changedTraining || !this.eventsRange) return;
      try {
        await this.getEventsForDates(
          this.eventsRange.start,
          this.eventsRange.end
        );
      } catch (error) {
        console.error(error);
      }
    },
    async getTemplates(): Promise<void> {
      const response = await api.get<EventTemplateResponse[]>("/api/templates");
      this.templates = response.data;
    },
    async createTemplate(template: EventTemplateRequest): Promise<void> {
      const request: EventTemplateRequest = {
        name: template.name,
        eventName: template.eventName,
        startTime: template.startTime,
        endTime: template.endTime,
        resourceTemplates: [...template.resourceTemplates],
      };
      await api.post("/api/templates", request);
      await this.getTemplates();
    },
    // TemplateForm sender requesten med id; id brukes i URL-en.
    async updateTemplate(
      template: EventTemplateRequest & { id: number }
    ): Promise<void> {
      await api.put(`/api/templates/${template.id}`, template);
      await this.getTemplates();
    },
    async deleteTemplate(
      template: Pick<EventTemplateResponse, "id">
    ): Promise<void> {
      await api.delete(`/api/templates/${template.id}`);
      await this.getTemplates();
    },
    // eventId kan være en streng når den kommer fra en route-param (EventPage).
    async createTemplateFromEvent(
      eventId: number | string,
      name: string
    ): Promise<void> {
      await api.post(`/api/events/${eventId}/template`, {
        name: name,
      } satisfies TemplateFromEventRequest);
      await this.getTemplates();
    },
    async createEventFromTemplate(
      templateId: number,
      date: string
    ): Promise<void> {
      const response = await api.post<EventResponse>(
        `/api/events/template/${templateId}`,
        {
          startDate: date,
        } satisfies EventFromTemplateRequest
      );
      this.events.push(response.data);
      recordLocalChange({ kind: "upsert", event: response.data });
    },
    async addResourceTypeFile(
      resourcetype: Pick<ResourceTypeResponse, "id">,
      fileInfo: { file: Blob; description: string }
    ): Promise<FileInfoResponse> {
      const formData = new FormData();
      formData.append("file", fileInfo.file);
      formData.append("description", fileInfo.description);
      const response = await api.post<FileInfoResponse>(
        `/api/resourcetypes/${resourcetype.id}/files`,
        formData
      );
      await this.getResourceTypes();
      return response.data;
    },
    async deleteResourceTypeFile(
      fileInfo: Pick<FileInfoResponse, "id" | "resourceTypeId">
    ): Promise<void> {
      await api.delete(
        `/api/resourcetypes/${fileInfo.resourceTypeId}/files/${fileInfo.id}`
      );
      await this.getResourceTypes();
    },
    async addMessage(
      eventResourceId: number,
      message: MessageRequest
    ): Promise<MessageResponse> {
      const response = await api.post<MessageResponse>(
        `/api/resources/${eventResourceId}/messages`,
        message
      );
      return response.data;
    },
    async deleteMessage(
      message: Pick<MessageResponse, "id" | "eventResourceId">
    ): Promise<void> {
      await api.delete(
        `/api/resources/${message.eventResourceId}/messages/${message.id}`
      );
    },
    // Kun admin. Serveren regner ut ny minimumStaff under ressurslås, så
    // samtidige klikk ikke overskriver hverandre. Svaret er hele ressursen med
    // flagg (isMissingStaff, isFull osv.), som legges i cachen via
    // applyResource og returneres.
    async addEmptySlot(eventResourceId: number): Promise<ResourceResponse> {
      const response = await api.post<ResourceResponse>(
        `/api/resources/${eventResourceId}/emptySlots`
      );
      this.applyResource(response.data);
      return response.data;
    },
    // Kun admin. Gir 400 hvis ressursen ikke har noen ledig plass å fjerne.
    async removeEmptySlot(eventResourceId: number): Promise<ResourceResponse> {
      const response = await api.delete<ResourceResponse>(
        `/api/resources/${eventResourceId}/emptySlots`
      );
      this.applyResource(response.data);
      return response.data;
    },
  },
});
