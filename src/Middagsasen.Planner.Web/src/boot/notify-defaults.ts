import { Notify } from "quasar";

Notify.setDefaults({
  color: "indigo-10",
  actions: [
    {
      icon: "close",
      color: "primary",
      round: true,
      flat: true,
      size: "sm",
    },
  ],
});
