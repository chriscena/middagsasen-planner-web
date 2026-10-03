import { defineStore } from "pinia";
import { api } from "boot/axios";
import { nextDay, toDateWire } from "src/shared/time";
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
  MinimumStaffRequest,
  ResourceResponse,
  ResourceTypeRequest,
  ResourceTypeResponse,
  SetTrainingRequest,
  ShiftResult,
  SignUpRequest,
  TemplateFromEventRequest,
} from "src/types";

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

// Løpenummer for getEventsForDates, slik at et tregt svar på en eldre
// forespørsel ikke overskriver events fra en nyere (f.eks. rask bla i uker).
let latestEventsRequest = 0;

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
      const startDate = encodeURIComponent(toDateWire(start));
      const endDate = encodeURIComponent(toDateWire(nextDay(end)));
      this.eventsRange = { start, end };
      const request = ++latestEventsRequest;
      const response = await api.get<EventResponse[]>(
        `/api/events?start=${startDate}&end=${endDate}`
      );
      if (request !== latestEventsRequest) return;
      this.events = response.data;
    },
    async addEvent(event: EventRequest): Promise<void> {
      const response = await api.post<EventResponse>("/api/events", event);
      this.events.push(response.data);
    },
    // id kan være en streng når den kommer fra en route-param (EventPage).
    async getEvent(id: number | string): Promise<void> {
      const response = await api.get<EventResponse>(`/api/events/${id}`);
      this.selectedEvent = response.data;
    },
    async deleteEvent(id: number): Promise<void> {
      await api.delete(`/api/events/${id}`);
      this.selectedEvent = null;
      this.events = this.events.filter((e) => e.id !== id);
    },
    async updateEvent(id: number | string, event: EventRequest): Promise<void> {
      const response = await api.put<EventResponse>(`/api/events/${id}`, event);
      const updatedEvent = response.data;
      // findIndex i stedet for indexOf(find(...)): samme resultat (-1 når den
      // ikke finnes), men typesikkert.
      const replaceIndex = this.events.findIndex(
        (event) => event.id === updatedEvent.id
      );
      if (replaceIndex > -1) this.events[replaceIndex] = updatedEvent;
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
      const response = await api.get<ResourceTypeResponse[]>(
        "/api/resourcetypes"
      );
      this.resourceTypes = response.data;
    },
    // --- Vakter ---
    // Alle skriveoperasjonene returnerer ShiftResult med hele ressursen etter
    // endringen (med flagg for innlogget bruker). Svaret legges i cachen med
    // applyShiftResult og returneres, så komponenten kan vise warnings.

    // Ta vakt. userId utelatt/null = innlogget bruker (annen bruker kun admin).
    async signUp(
      resourceId: number,
      request: SignUpRequest
    ): Promise<ShiftResult> {
      const response = await api.post<ShiftResult>(
        `/api/resources/${resourceId}/shifts`,
        request
      );
      const result = response.data;
      await this.applyShiftResult(result);
      return result;
    },
    // Endre tider, kommentar og (kun admin) eier. Endrer aldri opplæringen.
    async changeShift(
      shiftId: number,
      request: ChangeShiftRequest
    ): Promise<ShiftResult> {
      const response = await api.put<ShiftResult>(
        `/api/shifts/${shiftId}`,
        request
      );
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
    applyResource(updated: ResourceResponse): void {
      for (const event of this.events) {
        const resource = event.resources.find((r) => r.id === updated.id);
        if (resource) Object.assign(resource, updated);
      }
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
    // Kun admin. Svaret er hele ressursen med flagg (isMissingStaff, isFull
    // osv.), som legges i cachen via applyResource og returneres.
    async patchMinimumStaff(
      eventResourceId: number,
      minimumStaff: number
    ): Promise<ResourceResponse> {
      const response = await api.patch<ResourceResponse>(
        `/api/resources/${eventResourceId}/minimumStaff`,
        { minimumStaff } satisfies MinimumStaffRequest
      );
      this.applyResource(response.data);
      return response.data;
    },
  },
});
