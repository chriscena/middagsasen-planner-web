import { defineStore } from "pinia";
import { parseISO, formatISO, addDays, isBefore, isAfter } from "date-fns";
import { api } from "boot/axios";
import { useUserStore } from "src/stores/UserStore";

export const useEventStore = defineStore("events", {
  state: () => ({
    selectedEvent: null,
    events: [],
    resourceTypes: [],
    templates: [],
    eventStatuses: {},
  }),
  getters: {
    getEventsForDate: (state) => (timestamp) => {
      const start = new Date(timestamp.date);
      const end = addDays(start, 1);
      return state.events.filter(
        (e) =>
          isAfter(new Date(e.startTime), start) &&
          isBefore(new Date(e.startTime), end)
      );
    },
    eventStatusDates: (state) => [...Object.keys(state.eventStatuses)],
  },
  actions: {
    async getEventStatuses(month, year) {
      const response = await api.get(
        `/api/eventstatus?month=${month}&year=${year}`
      );
      const statuses = response.data;
      for (let index = 0; index < statuses.length; index++) {
        const status = statuses[index];
        this.eventStatuses[status.date] = status.isMissingStaff;
      }
    },
    async getEventsForDates(start, end) {
      var startDate = encodeURIComponent(
        formatISO(parseISO(start), { representation: "date" })
      );
      var endDate = encodeURI(
        formatISO(addDays(parseISO(end), 1), { representation: "date" })
      );
      const response = await api.get(
        `/api/events?start=${startDate}&end=${endDate}`
      );
      this.events = response.data;
    },
    async addEvent(event) {
      const response = await api.post("/api/events", event);
      this.events.push(response.data);
    },
    async getEvent(id) {
      const response = await api.get(`/api/events/${id}`);
      this.selectedEvent = response.data;
    },
    async deleteEvent(id) {
      await api.delete(`/api/events/${id}`);
      this.selectedEvent = null;
      this.events = this.events.filter((e) => e.id !== id);
    },
    async updateEvent(id, event) {
      const response = await api.put(`/api/events/${id}`, event);
      const updatedEvent = response.data;
      const replaceIndex = this.events.indexOf(
        this.events.find((event) => event.id === updatedEvent.id)
      );
      if (replaceIndex > -1) this.events[replaceIndex] = updatedEvent;
    },

    async createResourceType(resourceType) {
      const response = await api.post("/api/resourcetypes", resourceType);
      await this.getResourceTypes();
    },

    async updateResourceType(resourceType) {
      const response = await api.put(
        `/api/resourcetypes/${resourceType.id}`,
        resourceType
      );
      await this.getResourceTypes();
    },

    async deleteResourceType(resourceType) {
      const response = await api.delete(
        `/api/resourcetypes/${resourceType.id}`
      );
      await this.getResourceTypes();
    },
    async getResourceTypes() {
      // if (this.resourceTypes.length) return;
      const response = await api.get("/api/resourcetypes");
      this.resourceTypes = response.data;
    },
    async addTraining(resource, user, needTraining) {
      const model = {
        userId: user.id,
        startTime: resource.startTime,
        needTraining: needTraining,
      };
      const response = await api.post(
        `/api/resourcetypes/${resource.resourceType.id}/training`,
        model
      );
      const userStore = useUserStore();
      userStore.getUser();
    },
    async updateTraining(resource, training) {
      const model = {
        needTraining: !training.trainingComplete,
      };
      const response = await api.put(
        `/api/resourcetypes/${resource.resourceType.id}/training/${training.id}`,
        model
      );
      const userStore = useUserStore();
      userStore.getUser();
    },

    async addShift(parentResource, user, training) {
      const model = {
        startTime: parentResource.startTime,
        endTime: parentResource.endTime,
        userId: user.id,
      };
      const response = await api.post(
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
      if (training?.id) {
        await updateTraining(training);
      }
      if (training?.trainingComplete != null) {
        await this.addTraining(
          parentResource,
          user,
          !training?.trainingComplete
        );
      }
    },
    async deleteShift(shift) {
      const response = await api.delete(`/api/shifts/${shift.id}`);
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
    async updateShift(shift) {
      const response = await api.put(`/api/shifts/${shift.id}`, shift);
      const updatedShift = response.data;
      this.events.forEach((e) => {
        const resource = e.resources.find(
          (r) => r.id === updatedShift.eventResourceId
        );
        if (resource) {
          const shiftToUpdate = resource.shifts.find(
            (s) => s.id === updatedShift.id
          );
          shiftToUpdate.user = updatedShift.user;
          shiftToUpdate.startTime = updatedShift.startTime;
          shiftToUpdate.endTime = updatedShift.endTime;
          shiftToUpdate.comment = updatedShift.comment;
          return;
        }
      });
    },
    async getTemplates() {
      const response = await api.get("/api/templates");
      this.templates = response.data;
    },
    async createTemplate(template) {
      const request = {
        name: template.name,
        eventName: template.eventName,
        startTime: template.startTime,
        endTime: template.endTime,
        resourceTemplates: [...template.resourceTemplates],
      };
      await api.post("/api/templates", request);
      await this.getTemplates();
    },
    async updateTemplate(template) {
      await api.put(`/api/templates/${template.id}`, template);
      await this.getTemplates();
    },
    async deleteTemplate(template) {
      await api.delete(`/api/templates/${template.id}`);
      await this.getTemplates();
    },
    async createTemplateFromEvent(eventId, name) {
      await api.post(`/api/events/${eventId}/template`, {
        name: name,
      });
      await this.getTemplates();
    },
    async createEventFromTemplate(templateId, date) {
      const response = await api.post(`/api/events/template/${templateId}`, {
        startDate: date,
      });
      this.events.push(response.data);
    },
  },
});
