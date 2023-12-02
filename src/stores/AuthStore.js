import { defineStore } from "pinia";

export const useAuthStore = defineStore("auth", {
  state: () => ({
    user: null,
  }),

  getters: {
    accessToken(state) {
      return localStorage.getItem("access_token");
    },
    isAdmin(state) {
      return !!this.user?.isAdmin;
    },
  },

  actions: {
    setAccessToken(token) {
      localStorage.setItem("access_token", token);
    },
    setUser(user) {
      this.user = user;
    },
    removeUserSession() {
      localStorage.removeItem("access_token");
      this.user = null;
    },
  },
});
