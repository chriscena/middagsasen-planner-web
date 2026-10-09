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
  // events kan hentes på nytt etter en endring som påvirker flere oppgaver.
  eventsRange: { start: string; end: string } | null;
  // Løpenummer for getEventsForDates, slik at et tregt svar på en eldre
  // forespørsel ikke overskriver events fra en nyere (f.eks. rask bla i uker).
  latestEventsRequest: number;
  // Økes ved hver lokale endring i events (markEventsChanged), så
  // getEventsForDates ser at svaret kan være eldre enn endringen (#145).
  eventsChangeCount: number;
}

// Lagre en vakt med saveShift. shiftId null = ta ledig vakt på resourceId
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

// Hvor mange ganger getEventsForDates maksimalt henter når events endres
// lokalt underveis (se getEventsForDates).
const MAX_EVENTS_FETCHES = 3;

// Erstatter oppgaven med samme id (i alle events) med Object.assign, så
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

// Legger arrangementet i events, eller erstatter det med samme id. En
// henting som ble ferdig før svaret på opprettingen, kan allerede ha det.
function upsertEvent(events: EventResponse[], event: EventResponse): void {
  const index = events.findIndex((e) => e.id === event.id);
  if (index > -1) events[index] = event;
  else events.push(event);
}

// Oppdaterer beskjedene på oppgaven som er sendt inn (f.eks. dialogens
// selectedResource) og på oppgaven med samme id i events, hvis det er et
// annet objekt (events kan være hentet på nytt mens dialogen var åpen).
function updateMessages(
  events: EventResponse[],
  resource: ResourceResponse,
  update: (messages: MessageResponse[]) => MessageResponse[]
): void {
  resource.messages = update(resource.messages);
  for (const event of events) {
    for (const cached of event.resources) {
      if (cached.id === resource.id && cached !== resource)
        cached.messages = update(cached.messages);
    }
  }
}

// Felles henting av ett arrangement (getEvent og refreshEventResources).
async function fetchEvent(id: number): Promise<EventResponse> {
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
    latestEventsRequest: 0,
    eventsChangeCount: 0,
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
    // Endres events lokalt (ny, endret eller slettet vaktliste, vakt eller
    // beskjed) mens hentingen pågår, kan svaret være eldre enn endringen og
    // ville overskrevet den. Da hentes perioden på nytt i samme kall, inntil
    // MAX_EVENTS_FETCHES ganger (deretter brukes siste svar). Feil kastes
    // videre, og den som awaiter, venter på hele forløpet.
    async getEventsForDates(start: string, end: string): Promise<void> {
      const startDate = encodeURIComponent(toDayKey(start));
      const endDate = encodeURIComponent(toDayKey(nextDay(end)));
      this.eventsRange = { start, end };
      const request = ++this.latestEventsRequest;
      let events: EventResponse[] = [];
      for (let fetches = 0; fetches < MAX_EVENTS_FETCHES; fetches++) {
        const changeCount = this.eventsChangeCount;
        const response = await api.get<EventResponse[]>(
          `/api/events?start=${startDate}&end=${endDate}`
        );
        if (request !== this.latestEventsRequest) return;
        events = response.data;
        if (changeCount === this.eventsChangeCount) break;
      }
      this.events = events;
    },
    // Kalles av alle actions som endrer events lokalt (se getEventsForDates).
    markEventsChanged(): void {
      this.eventsChangeCount++;
    },
    async addEvent(event: EventRequest): Promise<void> {
      const response = await api.post<EventResponse>("/api/events", event);
      upsertEvent(this.events, response.data);
      this.markEventsChanged();
    },
    async getEvent(id: number): Promise<void> {
      // Nullstilles først, så en mislykket lasting aldri etterlater en
      // tidligere lastet vaktliste (som Slett ellers kunne slettet).
      this.selectedEvent = null;
      this.selectedEvent = await fetchEvent(id);
    },
    async deleteEvent(id: number): Promise<void> {
      await api.delete(`/api/events/${id}`);
      this.selectedEvent = null;
      this.events = this.events.filter((e) => e.id !== id);
      this.markEventsChanged();
    },
    async updateEvent(id: number, event: EventRequest): Promise<void> {
      const response = await api.put<EventResponse>(`/api/events/${id}`, event);
      const updatedEvent = response.data;
      // findIndex i stedet for indexOf(find(...)): samme resultat (-1 når den
      // ikke finnes), men typesikkert.
      const replaceIndex = this.events.findIndex(
        (event) => event.id === updatedEvent.id
      );
      if (replaceIndex > -1) this.events[replaceIndex] = updatedEvent;
      this.markEventsChanged();
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
    // Alle skriveoperasjonene returnerer ShiftResult med hele oppgaven etter
    // endringen (med flagg for innlogget bruker). Svaret legges i cachen med
    // applyShiftResult og returneres, så komponenten kan vise warnings.

    // Ta ledig vakt eller endre vakta i ett kall (én transaksjon i backend).
    // shiftId null = POST på oppgaven, ellers PUT på vakta. Felt som ikke er
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
    // Sette opplæringen til eieren av vakta på oppgavens vakttype.
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
    // Erstatter oppgaven med samme id (i alle events) med svaret fra serveren.
    // Object.assign, så objektet beholder identiteten: komponenter og dialoger
    // som holder på oppgaven (f.eks. selectedResource) ser de nye verdiene.
    applyResource(updated: ResourceResponse): void {
      assignResource(this.events, updated);
      this.markEventsChanged();
    },
    // Henter arrangementet på nytt og synkroniserer det i events-cachen med
    // svaret: oppgavelisten får serverens innhold og rekkefølge, slettede
    // oppgaver fjernes og nye legges til. Eksisterende oppgaver (og
    // arrangementet selv) oppdateres med Object.assign, så objektene beholder
    // identiteten (som i applyResource). Brukes for å rette opp utdaterte tall
    // etter at en operasjon er avvist fordi noen andre har endret oppgaven
    // (eller slettet den/arrangementet) i mellomtiden. Finnes ikke
    // arrangementet lenger (404), fjernes det fra cachen. selectedEvent røres
    // ikke: den er skjemadata for redigering og hentes på nytt av EventForm.
    async refreshEventResources(eventId: number): Promise<void> {
      let fresh: EventResponse;
      try {
        fresh = await fetchEvent(eventId);
      } catch (error) {
        if (getErrorResponse(error)?.status === 404) {
          this.events = this.events.filter((e) => e.id !== eventId);
          this.markEventsChanged();
          return;
        }
        throw error;
      }
      // Markeres også når arrangementet ikke er i cachen: en pågående henting
      // kan ha et eldre svar med det.
      this.markEventsChanged();
      const event = this.events.find((e) => e.id === eventId);
      if (!event) return;
      const existing = new Map(event.resources.map((r) => [r.id, r]));
      const resources = fresh.resources.map((r) => {
        const cached = existing.get(r.id);
        return cached ? Object.assign(cached, r) : r;
      });
      Object.assign(event, fresh, { resources });
    },
    // Legger svaret fra en vaktoperasjon i cachen via applyResource, og henter
    // deretter verdier serveren beregner utenfor den endrede oppgaven på nytt:
    // - Anleggskrav (event.competencyWarnings) beregnes for hele vaktlisten og
    //   påvirkes av alle påmeldinger, avmeldinger og endrede vakttider, så
    //   vaktlisten hentes på nytt (refreshEventResources).
    // - Ble en opplæring endret (changedTraining), kan flaggene på andre
    //   oppgaver av samme vakttype (mustAnswerTraining, needsTraining,
    //   canConfirmTraining osv.) i andre arrangementer også være endret, så
    //   hele perioden kalenderen viser hentes i stedet. Den dekker også
    //   vaktlisten, så den hentes ikke i tillegg.
    // Operasjonen har lyktes uansett, så feil i hentingen logges og svelges
    // (cachen er da bare ikke oppdatert utover den endrede oppgaven).
    async applyShiftResult(result: ShiftResult): Promise<void> {
      this.applyResource(result.resource);
      try {
        if (result.changedTraining && this.eventsRange) {
          await this.getEventsForDates(
            this.eventsRange.start,
            this.eventsRange.end
          );
        } else {
          await this.refreshEventResources(result.resource.eventId);
        }
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
        // Hele lista sendes (tom liste = ingen anleggskrav); utelatt = ingen.
        competencyRequirements: template.competencyRequirements
          ? [...template.competencyRequirements]
          : null,
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
    async createTemplateFromEvent(
      eventId: number,
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
      upsertEvent(this.events, response.data);
      this.markEventsChanged();
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
    // Beskjedene tar imot selve oppgaveobjektet (dialogens selectedResource)
    // og oppdaterer både det og oppgaven med samme id i events.
    async addMessage(
      resource: ResourceResponse,
      message: MessageRequest
    ): Promise<MessageResponse> {
      const response = await api.post<MessageResponse>(
        `/api/resources/${resource.id}/messages`,
        message
      );
      const added = response.data;
      updateMessages(this.events, resource, (messages) => [...messages, added]);
      this.markEventsChanged();
      return added;
    },
    async deleteMessage(
      resource: ResourceResponse,
      message: Pick<MessageResponse, "id">
    ): Promise<void> {
      await api.delete(`/api/resources/${resource.id}/messages/${message.id}`);
      updateMessages(this.events, resource, (messages) =>
        messages.filter((m) => m.id !== message.id)
      );
      this.markEventsChanged();
    },
    // Kun admin. Serveren regner ut ny shiftCount under lås på oppgaven, så
    // samtidige klikk ikke overskriver hverandre. Svaret er hele oppgaven med
    // flagg (isMissingStaff, isFull osv.), som legges i cachen via
    // applyResource og returneres.
    async addEmptySlot(eventResourceId: number): Promise<ResourceResponse> {
      const response = await api.post<ResourceResponse>(
        `/api/resources/${eventResourceId}/emptySlots`
      );
      this.applyResource(response.data);
      return response.data;
    },
    // Kun admin. Gir 400 hvis oppgaven ikke har noen ledig vakt å fjerne.
    async removeEmptySlot(eventResourceId: number): Promise<ResourceResponse> {
      const response = await api.delete<ResourceResponse>(
        `/api/resources/${eventResourceId}/emptySlots`
      );
      this.applyResource(response.data);
      return response.data;
    },
  },
});
