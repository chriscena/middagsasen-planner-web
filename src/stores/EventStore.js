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
    addEvent(event) {
      this.events.push(event);
    },
    getEvents() {
      //this.events.splice(0, this.events.length);
      if (this.events.length) return;
      this.events.push(...events);
    },
    getEventsForDates(start, end) {
      this.getEvents();
      var startDate = formatISO(parseISO(start));
      var endDate = formatISO(addDays(parseISO(end), 1));
      return this.events.filter(
        (e) => e.startTime >= startDate && e.startTime < endDate
      );
    },
    getResourceTypes() {
      //this.resourceTypes.splice(0, this.events.length);
      if (this.resourceTypes.length) return;
      this.resourceTypes.push(
        ...[
          {
            resourceTypeId: 1,
            name: "Heis voksen",
            sortOrder: 1,
          },
          {
            resourceTypeId: 2,
            name: "Heis barn",
            sortOrder: 2,
          },
          {
            resourceTypeId: 3,
            name: "Skiutleie",
            sortOrder: 3,
          },
          {
            resourceTypeId: 4,
            name: "Kiosk",
            sortOrder: 4,
          },
        ]
      );
    },
    updateUser(user) {
      this.events.forEach((e) =>
        e.resources.forEach((r) =>
          r.users.forEach((u) => {
            if (u.eventResourceUserId === user.eventResourceUserId) {
              u.comment = user.comment;
              return;
            }
          })
        )
      );
    },
    addUser(eventResourceId, user) {
      this.events.forEach((e) =>
        e.resources.forEach((r) => {
          if (r.eventResourceId == eventResourceId) {
            const newUser = Object.assign(
              { eventResourceUserId: uid(), comment: null },
              user
            );
            r.users.push(newUser);
            return;
          }
        })
      );
    },
    deleteUser(user) {
      this.events.forEach((e) =>
        e.resources.forEach((r) => {
          r.users = r.users.filter(
            (u) => u.eventResourceUserId !== user.eventResourceUserId
          );
        })
      );
    },
  },
});