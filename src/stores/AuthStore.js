import { defineStore } from "pinia";

const tokenItem = "access_token";
const userItem = "user";

export const useAuthStore = defineStore("auth", {
  state: () => ({ loggedInUser: null }),
  getters: {
    accessToken(state) {
      return localStorage.getItem(tokenItem);
    },
    user(state) {
      if (!this.loggedInUser) {
        var userJson = localStorage.getItem(userItem);
        this.loggedInUser = userJson ? JSON.parse(userJson) : null;
      }
      return this.loggedInUser;
    },
    isAdmin(state) {
      return this.user?.isAdmin;
    },
  },

  actions: {
    async setAccessToken(token) {
      localStorage.removeItem(tokenItem);
      localStorage.setItem(tokenItem, token);
      await new Promise((resolve) => setTimeout(resolve(), 100));
    },
    setUser(user) {
      localStorage.removeItem(userItem);
      localStorage.setItem(userItem, JSON.stringify(user));
      this.loggedInUser = user;
    },
    removeUserSession() {
      localStorage.removeItem(tokenItem);
      localStorage.removeItem(userItem);
      this.loggedInUser = null;
    },
  },
});
