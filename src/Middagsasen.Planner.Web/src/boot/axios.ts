import { defineBoot } from "#q-app";
import axios from "axios";
import { Notify } from "quasar";
import { useAuthStore } from "@/stores/AuthStore";
import { handleUnauthorized } from "@/auth/unauthorizedHandler";
import { applyRequestDefaults } from "@/shared/requestDefaults";

// Be careful when using SSR for cross-request state pollution
// due to creating a Singleton instance here;
// If any client changes this (global) instance, it might be a
// good idea to move this instance creation inside of the
// "export default () => {}" function below (which runs individually
// for each client)
const api = axios.create(/*{ baseURL: "https://api.example.com" }*/);

api.interceptors.request.use(
  function (config) {
    return applyRequestDefaults(config, localStorage.getItem("access_token"));
  },
  function (error) {
    console.log(error);
    return Promise.reject(error);
  }
);

export default defineBoot(({ router }) => {
  api.interceptors.response.use(
    (response) => response,
    (error) =>
      handleUnauthorized(error, {
        authStore: useAuthStore(),
        router,
        notify: (opts) => Notify.create(opts),
      })
  );
});

export { api };
