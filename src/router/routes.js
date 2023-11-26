import { formatISO } from "date-fns";

const routes = [
  {
    path: "/",
    component: () => import("layouts/MainLayout.vue"),
    children: [
      {
        path: "",
        redirect: `/day/${formatISO(new Date(), { representation: "date" })}`,
      },
      {
        path: "day/:date",
        component: () => import("pages/IndexPage.vue"),
        props: true,
      },
      { path: "create", component: () => import("pages/EventPage.vue") },
    ],
  },

  // Always leave this as last one,
  // but you can also remove it
  {
    path: "/:catchAll(.*)*",
    component: () => import("pages/ErrorNotFound.vue"),
  },
];

export default routes;
