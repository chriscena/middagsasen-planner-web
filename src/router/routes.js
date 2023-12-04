import { formatISO } from "date-fns";

const routes = [
  {
    path: "/",
    component: () => import("layouts/MainLayout.vue"),
    children: [
      {
        path: "login",
        component: () => import("pages/LoginPage.vue"),
      },
      {
        path: "",
        redirect: `/day/${formatISO(new Date(), { representation: "date" })}`,
      },
      {
        path: "day/:date",
        component: () => import("pages/IndexPage.vue"),
        props: true,
      },
      {
        path: "create",
        redirect: `/create/${formatISO(new Date(), {
          representation: "date",
        })}`,
      },
      {
        path: "create/:date",
        component: () => import("pages/EventPage.vue"),
        props: true,
      },
      {
        path: "resourceTypes",
        component: () => import("pages/ResourceTypesPage.vue"),
      },
      {
        path: "phonelist",
        component: () => import("pages/PhoneListPage.vue"),
      },
      {
        path: "users",
        component: () => import("pages/UsersPage.vue"),
      },
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
