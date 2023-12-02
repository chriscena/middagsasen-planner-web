import { defineStore } from "pinia";
import { uid } from "quasar";
import {
  parseISO,
  format,
  addHours,
  addMinutes,
  formatISO,
  addDays,
  isBefore,
  isAfter,
} from "date-fns";
import { api } from "boot/axios";

const today = new Date();

const events = [
  {
    eventId: uid(),
    eventName: "Helg",
    startTime: formatISO(today, { representation: "date" }) + "T10:00:00",
    endTime: formatISO(today, { representation: "date" }) + "T17:00:00",
    resources: [
      {
        eventResourceId: uid(),
        resourceType: { resourceTypeId: 1, name: "Heis voksen" },
        startTime: formatISO(today, { representation: "date" }) + "T09:30:00",
        endTime: formatISO(today, { representation: "date" }) + "T17:30:00",
        minimumStaff: 2,
        users: [
          {
            eventResourceUserId: uid(),
            userId: 1,
            name: "Christoffer Cena",
            phoneNumber: "91305023",
            comment: "Test",
          },
          {
            eventResourceUserId: uid(),
            userId: 2,
            name: "Stina Gryhn",
            phoneNumber: "12345678",
            comment: null,
          },
        ],
      },
      {
        eventResourceId: uid(),
        resourceType: { resourceTypeId: 2, name: "Heis barn" },
        startTime: formatISO(today, { representation: "date" }) + "T09:30:00",
        endTime: formatISO(today, { representation: "date" }) + "T17:30:00",
        minimumStaff: 1,
        users: [],
      },
      {
        eventResourceId: uid(),
        resourceType: { resourceTypeId: 4, name: "Kiosk" },
        startTime: formatISO(today, { representation: "date" }) + "T09:30:00",
        endTime: formatISO(today, { representation: "date" }) + "T17:30:00",
        minimumStaff: 3,
        users: [],
      },
    ],
  },
];

export const useEventStore = defineStore("events", {
  state: () => ({
    events: [],
    resourceTypes: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    async addEvent(event) {
      const response = await api.post("/api/events", event);
    },
    getEvents() {
      //this.events.splice(0, this.events.length);
      if (this.events.length) return;
      this.events.push(...events);
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
    updateUser(user) {
      this.events.forEach((e) =>
        e.resources.forEach((r) =>
          r.shifts.forEach((u) => {
            if (u.eventResourceUserId === user.eventResourceUserId) {
              u.comment = user.comment;
              return;
            }
          })
        )
      );
    },
    async addShift(parentResource, user) {
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
    },
    async deleteShift(shift) {
      const response = await api.delete(`/api/shifts/${shift.id}`);
      const deletedShift = response.data;
      this.events.forEach((e) => {
        const resource = e.resources.find(
          (r) => r.id === deletedShift.eventResourceId
        );
        resource.shifts = resource.shifts.filter(
          (u) => u.id !== deletedShift.id
        );
      });
    },
  },
});
