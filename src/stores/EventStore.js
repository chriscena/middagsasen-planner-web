import { defineStore } from "pinia";

const events = [
  {
    eventId: 1,
    eventName: "Helg",
    startTime: "2023-11-25T10:00:00",
    endTime: "2023-11-25T14:00:00",
    resources: [
      {
        eventResourceId: 1,
        resourceTypeId: 1,
        resourceTypeName: "Heis voksen",
        startTime: "2023-11-25T09:00:00",
        endTime: "2023-11-25T15:00:00",
        minimumStaff: 2,
        users: [
          {
            eventResourceUserId: 1,
            userId: 1,
            name: "Christoffer Cena",
            phoneNumber: "91305023",
            comment: "Test",
          },
          {
            eventResourceUserId: 2,
            userId: 2,
            name: "Test Cena",
            phoneNumber: "12345678",
            comment: null,
          },
        ],
      },
      {
        eventResourceId: 2,
        resourceTypeId: 2,
        resourceTypeName: "Heis barn",
        startTime: "2023-11-25T09:00:00",
        endTime: "2023-11-25T15:00:00",
        minimumStaff: 2,
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
      this.events.splice(0, this.events.length);
      this.events.push(...events);
    },
    getResourceTypes() {
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
  },
});
