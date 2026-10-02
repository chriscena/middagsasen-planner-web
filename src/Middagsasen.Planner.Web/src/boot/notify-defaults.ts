import { Notify } from "quasar";

Notify.setDefaults({
  color: "indigo-10",
  actions: [
    {
      icon: "close",
      // Ingen farge: den flate knappen arver varselets tekstfarge (hvit på
      // mørke/fargede varsler, mørk på type "warning" med gul bakgrunn).
      round: true,
      flat: true,
      size: "sm",
    },
  ],
});
