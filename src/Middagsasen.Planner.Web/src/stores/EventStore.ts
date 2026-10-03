import { defineStore } from "pinia";
import { parseISO, formatISO, addDays } from "date-fns";
import type { AxiosResponse } from "axios";
import { api } from "boot/axios";
import { useAuthStore } from "src/stores/AuthStore";
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
  MinimumStaffResponse,
  ResourceTypeRequest,
  ResourceTypeResponse,
  SetTrainingRequest,
  ShiftResult,
  SignUpRequest,
  TemplateFromEventRequest,
  TrainingResponse,
} from "src/types";

interface EventState {
  selectedEvent: EventResponse | null;
  events: EventResponse[];
  resourceTypes: ResourceTypeResponse[];
  templates: EventTemplateResponse[];
  // Dato (yyyy-MM-dd) -> om vaktlistene den dagen mangler mannskap.
  eventStatuses: Record<string, boolean>;
}

// Erstatter (eller legger til) opplæringen for samme ressurstype. Ny liste, så
// det ikke muteres et objekt som deles med andre.
function upsertTraining<T extends Pick<TrainingResponse, "resourceTypeId">>(
  trainings: T[],
  training: T
): T[] {
  return [
    ...trainings.filter((t) => t.resourceTypeId !== training.resourceTypeId),
    training,
  ];
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
      const startDate = encodeURIComponent(
        formatISO(parseISO(start), { representation: "date" })
      );
      const endDate = encodeURI(
        formatISO(addDays(parseISO(end), 1), { representation: "date" })
      );
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
      // Opplæringen gjelder brukeren som ble satt opp.
      const userId = request.userId ?? useAuthStore().user?.id ?? null;
      this.applyShiftResult(result, userId);
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
      this.applyShiftResult(result);
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
      // Opplæringen gjelder alltid eieren av vakta, som finnes i svaret.
      const owner = result.resource.shifts.find((s) => s.id === shiftId)?.user;
      this.applyShiftResult(result, owner?.id ?? null);
      return result;
    },
    // Trekke seg fra / slette vakta.
    async withdraw(shiftId: number): Promise<ShiftResult> {
      const response = await api.delete<ShiftResult>(`/api/shifts/${shiftId}`);
      const result = response.data;
      this.applyShiftResult(result);
      return result;
    },
    // Legger svaret fra en vaktoperasjon i cachen.
    // - Ressursen med samme id (i alle events) oppdateres med Object.assign, så
    //   objektet beholder identiteten: komponenter og dialoger som holder på
    //   ressursen (f.eks. selectedResource) ser de nye verdiene.
    // - changedTraining gjelder alle ressurser av samme ressurstype. Den har
    //   ingen userId, så kalleren sender inn brukeren den gjelder
    //   (trainingUserId). Uten bruker oppdateres bare ressursen selv.
    applyShiftResult(
      result: ShiftResult,
      trainingUserId: number | null = null
    ): void {
      const updated = result.resource;
      for (const event of this.events) {
        const resource = event.resources.find((r) => r.id === updated.id);
        if (resource) Object.assign(resource, updated);
      }

      const training = result.changedTraining;
      if (!training || trainingUserId == null) return;

      const authStore = useAuthStore();
      const currentUser = authStore.user;
      const isCurrentUser = currentUser?.id === trainingUserId;
      const needsTraining = training.trainingComplete === false;

      for (const event of this.events) {
        for (const resource of event.resources) {
          if (resource.resourceType.id !== training.resourceTypeId) continue;
          // Brukeren har nå en opplæringsrad for ressurstypen.
          if (isCurrentUser) resource.mustAnswerTraining = false;
          for (const shift of resource.shifts) {
            if (shift.user.id !== trainingUserId) continue;
            shift.needsTraining = needsTraining;
            // canConfirmTraining krever needsTraining. Blir opplæringen ønsket,
            // kan ikke flagget utledes her (trener/fortid), så det står urørt.
            if (!needsTraining) shift.canConfirmTraining = false;
            shift.user.trainings = upsertTraining(
              shift.user.trainings,
              training
            );
          }
        }
      }

      if (isCurrentUser && currentUser) {
        authStore.setUser({
          ...currentUser,
          trainings: upsertTraining(currentUser.trainings ?? [], training),
        });
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
    // Returnerer hele axios-responsen (kallerne leser res.data). eventResourceId
    // sendes også i body, men brukes kun i URL-en.
    async patchMinimumStaff(
      model: MinimumStaffRequest & { eventResourceId: number }
    ): Promise<AxiosResponse<MinimumStaffResponse>> {
      return await api.patch<MinimumStaffResponse>(
        `/api/resources/${model.eventResourceId}/minimumStaff`,
        model
      );
    },
  },
});
