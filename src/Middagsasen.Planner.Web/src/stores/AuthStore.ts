import { defineStore } from "pinia";
import type { UserResponse } from "@/types";

const tokenItem = "access_token";
const userItem = "user";

interface AuthState {
  loggedInUser: UserResponse | null;
}

export const useAuthStore = defineStore("auth", {
  state: (): AuthState => ({ loggedInUser: null }),
  getters: {
    accessToken(): string | null {
      return localStorage.getItem(tokenItem);
    },
    user(): UserResponse | null {
      if (!this.loggedInUser) {
        const userJson = localStorage.getItem(userItem);
        this.loggedInUser = userJson
          ? (JSON.parse(userJson) as UserResponse)
          : null;
      }
      return this.loggedInUser;
    },
    isAdmin(): boolean | undefined {
      return this.user?.isAdmin;
    },
  },

  actions: {
    async setAccessToken(token: string): Promise<void> {
      localStorage.removeItem(tokenItem);
      localStorage.setItem(tokenItem, token);
      // @ts-expect-error resolve() kalles umiddelbart og setTimeout får undefined,
      // så det er ingen reell ventetid. Atferden er bevart fra JS-versjonen.
      await new Promise<void>((resolve) => setTimeout(resolve(), 100));
    },
    setUser(user: UserResponse): void {
      localStorage.removeItem(userItem);
      localStorage.setItem(userItem, JSON.stringify(user));
      this.loggedInUser = user;
    },
    removeUserSession(): void {
      localStorage.removeItem(tokenItem);
      localStorage.removeItem(userItem);
      this.loggedInUser = null;
    },
  },
});
