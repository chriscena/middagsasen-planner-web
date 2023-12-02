import { defineStore } from "pinia";
import { api } from "boot/axios";
import { useAuthStore } from "src/stores/AuthStore";

const authStore = useAuthStore();

export const useUserStore = defineStore("users", {
  state: () => ({
    users: [],
    phoneList: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    async getUser() {
      const userResponse = await api.get("/api/me");
      authStore.setUser(userResponse.data);
    },
    async getUsers() {
      const response = await api.get("/api/users");
      this.users.splice(0, this.users.length);
      this.users.push(...response.data);
    },
    async saveUser(user) {
      const response = await api.put("/api/me", user);
      authStore.setUser(response.data);
    },
    async getPhoneList() {
      const response = await api.get("/api/users/phone");
      this.phoneList = response.data;
    },
    async logout() {
      await api.post("/api/authentication/logout");
      authStore.removeUserSession();
    },
  },
});
