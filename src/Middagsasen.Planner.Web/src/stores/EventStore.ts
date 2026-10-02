import { defineStore } from "pinia";
import { parseISO, formatISO, addDays } from "date-fns";
import type { AxiosResponse } from "axios";
import { api } from "boot/axios";
import { useUserStore } from "src/stores/UserStore";
import type {
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
  ResourceResponse,
  ResourceTypeRequest,
  ResourceTypeResponse,
  ShiftRequest,
  ShiftResponse,
  TemplateFromEventRequest,
  TrainingRequest,
  TrainingResponse,
  UserResponse,
} from "src/types";

interface EventState {
  selectedEvent: EventResponse | null;
  events: EventResponse[];
  resourceTypes: ResourceTypeResponse[];
  templates: EventTemplateResponse[];
  // Dato (yyyy-MM-dd) -> om vaktlistene den dagen mangler mannskap.
  eventStatuses: Record<string, boolean>;
}

// Opplæringsstatus slik EventItemCard sender den: enten en TrainingResponse
// fra brukeren eller en tom plassholder ({ id: 0, trainingComplete: null }).
type ShiftTraining = Pick<TrainingResponse, "id" | "trainingComplete">;

// TrainingRequest slik den faktisk sendes: confirmedBy settes av backend
// (CurrentUser) og sendes ikke, selv om DTO-en har feltet som påkrevd.
type ShiftTrainingRequest = Omit<TrainingRequest, "confirmedBy">;
type ShiftModel = Omit<ShiftRequest, "training"> & {
  training: ShiftTrainingRequest | null;
};

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
    eventStatusDates: (state): string[] => [...Object.keys(state.eventStatuses)],
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
      const response = await api.get<EventResponse[]>(
        `/api/events?start=${startDate}&end=${endDate}`
      );
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
      // OpenAPI oppgir EventTemplateResponse (feil [ProducesResponseType]),
      // men backend returnerer ResourceTypeResponse.
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
    async addTraining(
      resource: ResourceResponse,
      user: Pick<UserResponse, "id">,
      needTraining: boolean
    ): Promise<void> {
      // id og confirmedBy er påkrevd i TrainingRequest, men sendes ikke
      // (confirmedBy settes av backend).
      const model: Omit<TrainingRequest, "id" | "confirmedBy"> = {
        userId: user.id,
        resourceTypeId: resource.resourceType.id,
        startTime: resource.startTime,
        trainingCompleted: !needTraining,
      };
      await api.post(
        `/api/resourcetypes/${resource.resourceType.id}/training`,
        model
      );
      const userStore = useUserStore();
      userStore.getUser();
    },
    // NB: Backend har ikke noe PUT-endepunkt for
    // /api/resourcetypes/{id}/training/{trainingId}, og body-en matcher ingen
    // DTO. Metoden kalles ikke fra noen komponent i dag.
    async updateTraining(
      resource: ResourceResponse,
      training: ShiftTraining
    ): Promise<void> {
      const model = {
        needTraining: !training.trainingComplete,
      };
      await api.put(
        `/api/resourcetypes/${resource.resourceType.id}/training/${training.id}`,
        model
      );
      const userStore = useUserStore();
      userStore.getUser();
    },

    async addShift(
      parentResource: ResourceResponse,
      user: Pick<UserResponse, "id">,
      comment: string | null,
      training: ShiftTraining
    ): Promise<void> {
      const model: ShiftModel = {
        startTime: parentResource.startTime,
        endTime: parentResource.endTime,
        userId: user.id,
        comment: comment,
        training:
          training?.trainingComplete == null
            ? null
            : {
                id: training.id,
                resourceTypeId: parentResource.resourceType.id,
                userId: user.id,
                startTime: parentResource.startTime,
                trainingCompleted: training.trainingComplete,
              },
      };
      const response = await api.post<ShiftResponse>(
        `/api/resources/${parentResource.id}/shifts`,
        model
      );

      const newShift = response.data;

      this.events.forEach((e) => {
        const resource = e.resources.find(
          (r) => r.id === newShift.eventResourceId
        );
        if (resource) {
          resource.shifts.push(newShift);
          return;
        }
      });

      if (training.trainingComplete != null) {
        const userStore = useUserStore();
        userStore.getUser();
      }
      // if (training?.id) {
      //   await updateTraining(training);
      // }
    },
    async deleteShift(shift: Pick<ShiftResponse, "id">): Promise<void> {
      const response = await api.delete<ShiftResponse>(
        `/api/shifts/${shift.id}`
      );
      const deletedShift = response.data;
      this.events.forEach((e) => {
        const resource = e.resources.find(
          (r) => r.id === deletedShift.eventResourceId
        );
        if (resource) {
          resource.shifts = resource.shifts.filter(
            (u) => u.id !== deletedShift.id
          );
          return;
        }
      });
    },
    async updateShift(
      parentResource: ResourceResponse,
      shift: ShiftResponse,
      training: ShiftTraining | null
    ): Promise<void> {
      console.log(shift);
      // `?? null`: feltene er valgfrie i ShiftResponse, og med
      // exactOptionalPropertyTypes kan ikke undefined tilordnes direkte. Backend
      // sender alltid null fremfor å utelate feltet, så verdien er den samme.
      const model: ShiftModel = {
        startTime: shift.startTime ?? null,
        endTime: shift.endTime ?? null,
        userId: shift.user.id,
        comment: shift.comment ?? null,
        training:
          training?.trainingComplete == null
            ? null
            : {
                id: training.id,
                resourceTypeId: parentResource.resourceType.id,
                userId: shift.user.id,
                startTime: parentResource.startTime,
                trainingCompleted: training.trainingComplete,
              },
      };
      const response = await api.put<ShiftResponse>(
        `/api/shifts/${shift.id}`,
        model
      );
      const updatedShift = response.data;
      this.events.forEach((e) => {
        const resource = e.resources.find(
          (r) => r.id === updatedShift.eventResourceId
        );
        if (resource) {
          // Non-null assertion bevarer JS-atferden: kaster hvis vakten ikke
          // finnes lokalt.
          const shiftToUpdate = resource.shifts.find(
            (s) => s.id === updatedShift.id
          )!;
          shiftToUpdate.user = updatedShift.user;
          shiftToUpdate.startTime = updatedShift.startTime ?? null;
          shiftToUpdate.endTime = updatedShift.endTime ?? null;
          shiftToUpdate.comment = updatedShift.comment ?? null;
          shiftToUpdate.needsTraining = updatedShift.needsTraining;
          return;
        }
      });

      if (training?.trainingComplete != null) {
        const userStore = useUserStore();
        userStore.getUser();
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
    // Kun `message` sendes: eventResourceId tas fra URL-en og createdBy settes
    // av backend, selv om MessageRequest har dem som påkrevd.
    async addMessage(
      eventResourceId: number,
      message: Pick<MessageRequest, "message">
    ): Promise<MessageResponse> {
      // OpenAPI oppgir ShiftResponse (feil [ProducesResponseType]), men
      // backend returnerer MessageResponse.
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
