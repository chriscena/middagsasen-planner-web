import { defineStore } from "pinia";
import { api } from "boot/axios";
import { useAuthStore } from "src/stores/AuthStore";
import { useRouter } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();

api.interceptors.response.use(
  function (response) {
    return response;
  },
  function (error) {
    if (error.response.status === 401) {
      window.location("/login");
      return Promise.reject("Unauthorized");
    }
    // Any status codes that falls outside the range of 2xx cause this function to trigger
    // Do something with response error
    return Promise.reject(error);
  }
);

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
      try {
        const userResponse = await api.get("/api/me");
        authStore.setUser(userResponse.data);
      } catch (error) {
        if (error?.response?.status === 401) router.replace("/login");
      }
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
      try {
        await api.post("/api/authentication/logout");
      } catch (error) {}
      authStore.removeUserSession();
    },
  },
});
