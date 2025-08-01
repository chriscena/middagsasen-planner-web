import { defineBoot } from '#q-app/wrappers'
import VuePlugin from '@quasar/quasar-ui-qcalendar/QCalendarAgenda'
import '@quasar/quasar-ui-qcalendar/QCalendarAgenda.css'

export default defineBoot(({ app }) => {
  app.use(VuePlugin)
})
