import { defineBoot } from "#q-app";
import { VueDatePicker } from "@vuepic/vue-datepicker";
import "@vuepic/vue-datepicker/dist/main.css";

export default defineBoot(({ app }) => {
  app.component("VueDatePicker", VueDatePicker);
});
