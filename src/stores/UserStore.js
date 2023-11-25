import { defineStore } from "pinia";

const users = [
  {
    userId: 1,
    userName: "91305023",
    firstName: "Chris",
    lastName: "Cena",
    IsAdmin: false,
  },
  {
    userId: 2,
    userName: "12345678",
    firstName: "Stina",
    lastName: "Gryhn",
    IsAdmin: false,
  },
  {
    userId: 3,
    userName: "12345678",
    firstName: "Rita",
    lastName: "Lin",
    IsAdmin: false,
  },
  {
    userId: 4,
    userName: "87654321",
    firstName: "Vegar",
    lastName: "Beider",
    IsAdmin: false,
  },
];

export const useUserStore = defineStore("users", {
  state: () => ({
    user: null,
    users: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    getUser() {
      this.user = {
        userId: 1,
        userName: "91305023",
        firstName: "Chris",
        lastName: "Cena",
        name: "Chris Cena",
        IsAdmin: false,
      };
    },
    getUsers() {
      this.users.splice(0, this.users.length);
      this.users.push(...this.users);
    },
  },
});
