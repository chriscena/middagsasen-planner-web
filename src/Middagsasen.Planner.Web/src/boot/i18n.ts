import { defineBoot } from "#q-app/wrappers";
import { createI18n } from "vue-i18n";
import messages, { type MessageSchema } from "src/i18n";

export default defineBoot(({ app }) => {
  const i18n = createI18n<[MessageSchema], "en-US">({
    locale: "en-US",
    globalInjection: true,
    messages,
  });

  // Set i18n instance on app
  app.use(i18n);
});
