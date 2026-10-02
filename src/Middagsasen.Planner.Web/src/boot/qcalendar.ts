import { defineBoot } from '#q-app/wrappers'
import VuePlugin from '@quasar/quasar-ui-qcalendar/QCalendarAgenda'
import '@quasar/quasar-ui-qcalendar/QCalendarAgenda.css'

export default defineBoot(({ app }) => {
  // @ts-expect-error Pakkens typer for "./QCalendarAgenda" peker på komponenten,
  // men runtime-default (dist/QCalendarAgenda.esm.js) er pluginen med install().
  app.use(VuePlugin)
})
