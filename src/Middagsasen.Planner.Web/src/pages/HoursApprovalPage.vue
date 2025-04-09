<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="menu"
          @click="emit('toggle-left')"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Timeføring</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
          title="Din brukerinfo"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <div>
      <q-table
        v-if="isAdmin"
        flat
        row-key="workHourId"
        ref="tableRef"
        :columns="columns"
        :visible-columns="visibleColumns"
        :loading="loading"
        :rows="userWorkHours"
        style="max-height: 85vh"
        no-data-label="Ingen data"
        class="sticky-header-table"
        v-model:pagination="pagination"
        @request="getUserWorkHours"
        :selected-rows-label="getSelectedString"
        selection="multiple"
        v-model:selected="selectedWorkHours"
        :filter="filter"
      >
        <template #top-left>
          <div class="row q-gutter-sm q-pr-md">
            <q-radio
              :disable="loading"
              :model-value="approvedFilter"
              label="Ikke godkjent"
              :val="2"
              @update:model-value="(val) => setFilter({ approved: val })"
            />
            <q-radio
              :disable="loading"
              :model-value="approvedFilter"
              label="Godkjent"
              :val="1"
              @update:model-value="(val) => setFilter({ approved: val })"
            />
          </div>
        </template>
        <template #header-selection="props">
          <q-checkbox
            v-if="isAdmin && approvedFilter === 2"
            :props="props"
            v-model="props.selected"
            class="selection_width"
          />
        </template>
        <template #body-selection="props">
          <q-checkbox
            v-if="props.row.approvedBy == null && isAdmin"
            :props="props"
            v-model="props.selected"
          />
        </template>
        <template #body-cell-approved="props">
          <q-td :props="props">
            <q-icon
              size="md"
              name="check_circle"
              class="green-text"
              v-if="props.row.approvedBy != null"
            />
            <q-icon
              size="md"
              name="cancel"
              class="red-text"
              v-if="props.row.approvedBy == null"
            />
          </q-td>
        </template>
        <template #body-cell-from="props">
          <q-td :props="props">
            <span v-if="props.row.startTime">
              {{ toTimeString(props.row.startTime) }}
            </span>
            <q-separator></q-separator>
            <span v-if="props.row.startTime">
              {{ toDateString(props.row.startTime) }}
            </span>
          </q-td>
        </template>
        <template #body-cell-to="props">
          <q-td :props="props">
            <span v-if="props.row.endTime">
              {{ toTimeString(props.row.endTime) }}
            </span>
            <q-separator></q-separator>
            <span v-if="props.row.endTime">
              {{ toDateString(props.row.endTime) }}
            </span>
          </q-td>
        </template>
      </q-table>
      <span class="q-pr-xl row">
        <q-space></q-space>
        <q-btn
          v-if="isAdmin"
          :disable="!(selectedWorkHours.length > 0)"
          label="Approve"
          size="large"
          @click="approveUpdateRows"
          color="primary"
        />
        <q-space></q-space>
      </span>
    </div>
  </q-page>
</template>
<script setup>
import { useQuasar } from "quasar";
import { onMounted, ref, computed, useTemplateRef } from "vue";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { format } from "date-fns";
import { useRoute, useRouter } from "vue-router";

// store init
const $router = useRouter();
const $route = useRoute();
const workHourStore = useWorkHourStore();
const userStore = useUserStore();
const authStore = useAuthStore();
const $q = useQuasar();

// props and emits
const emit = defineEmits(["toggle-right", "toggle-left"]);

// refs
const selected = ref([]);
const selectedWorkHours = ref([]);
const tableRef = useTemplateRef("tableRef");
const loading = ref(false);
const userWorkHours = ref([]);
const currentPage = ref(1);
const currentUser = computed(() => authStore.user);
const currentUserId = currentUser.value.id;
const pagination = ref({
  rowsPerPage: Number.isInteger(parseInt($route.query.rowPP))
    ? parseInt($route.query.rowPP)
    : 20,
  page: Number.isInteger(parseInt($route.query.page))
    ? parseInt($route.query.page)
    : 1,
  rowsNumber: undefined,
});

// constants
const columns = [
  {
    name: "approved",
    label: "Godkjent",
    field: (row) => row.approvedBy,
    align: "left",
    headerStyle: "width: 20%",
    style: "width: 20%",
  },
  {
    name: "user",
    label: "Bruker",
    field: (row) => row.userId,
    format: (val) => approvedByName(val),
    align: "left",
    headerStyle: "width: 20%",
    style: "width: 20%",
  },
  {
    name: "approvedBy",
    label: "Godkjent av",
    field: (row) => row.approvedBy,
    format: (val) => approvedByName(val),
    align: "left",
    headerStyle: "width: 20%",
    style: "width: 20%",
  },
  {
    name: "description",
    label: "Beskrivelse",
    field: (row) => row.description,
    align: "left",
    headerStyle: "width: 30%",
    style:
      "width: 20%; max-width: 100px; text-overflow: ellipsis; overflow: hidden;",
  },
  {
    name: "from",
    label: "Fra",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "to",
    label: "Til",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "hours",
    label: "Timer",
    field: (row) => row.hours,
    format: (val) => val.toFixed(2),
    align: "left",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
];

// computed
const visibleColumns = computed(() => {
  let cols = [];
  cols.push("user");
  if ($q.screen.gt.xs) cols.push("approved");
  if ($q.screen.gt.xs && approvedFilter.value === 1) cols.push("approvedBy");
  if ($q.screen.gt.sm) cols.push("description");
  cols.push("from");
  cols.push("to");
  if ($q.screen.gt.sm) cols.push("hours");
  return cols;
});

const isAdmin = computed(() => currentUser.value?.isAdmin ?? false);

const page = computed(() => {
  return $route.query.page;
});
const rowPP = computed(() => {
  return $route.query.rowPP;
});

const approvedFilter = computed(() => {
  return $route.query.a !== undefined ? parseInt($route.query.a) : 2;
});

const filter = computed(() => {
  return {
    approved: approvedFilter.value,
  };
});

// methods
function resetTable() {
  userWorkHours.value = [];
  currentPage.value = 1;
  pagination.value.rowsNumber = undefined;
}

async function getUserWorkHours(props) {
  const filter = props.filter;
  loading.value = true;
  resetTable();
  try {
    const params = {
      approved: filter.approved,
      page: props.pagination.page,
      pageSize: props.pagination.rowsPerPage,
    };
    await workHourStore.getWorkHoursByUser(currentUserId, params);
    userWorkHours.value = workHourStore.userWorkHours.result;
    pagination.value.rowsNumber = workHourStore.userWorkHours.totalCount;
    pagination.value.page = props.pagination.page;
    pagination.value.rowsPerPage = props.pagination.rowsPerPage;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    setFilter({
      page: pagination.value.page,
      rowsPP: pagination.value.rowsPerPage,
    });
    loading.value = false;
  }
}

async function approveUpdateRows() {
  loading.value = true;
  try {
    for (let i = 0; i < selectedWorkHours.value.length; i++) {
      try {
        const model = {
          approvedBy: currentUserId,
          workHourId: selectedWorkHours.value[i].workHourId,
        };
        await workHourStore.updateApprovedBy(model);
      } catch (e) {
        console.error(e);
        $q.notify({
          type: "negative",
          closeBtn: "close",
          message: ("errorOccurred", { error: e }),
        });
      }
    }
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    loading.value = false;
    selectedWorkHours.value = [];
    tableRef.value?.requestServerInteraction();
  }
}

async function setFilter(filter) {
  console.log(filter);
  if (!filter) {
    await $router.push({
      query: {},
    });
  } else {
    await $router.push({
      query: {
        a:
          filter.approved !== undefined || approvedFilter.value !== undefined
            ? filter.approved ?? approvedFilter.value
              ? filter.approved ?? approvedFilter.value
              : undefined
            : undefined,
        page:
          pagination.value.page !== undefined || page.value !== undefined
            ? pagination.value.page ?? page.value
              ? pagination.value.page
              : 1
            : undefined,
        rowPP:
          pagination.value.rowsPerPage !== undefined ||
          rowPP.value !== undefined
            ? pagination.value.rowsPerPage ?? rowPP.value
              ? pagination.value.rowsPerPage
              : undefined
            : undefined,
      },
    });
  }
}

function toDateTimeString(value, options) {
  let timeFormat = "dd.MM.yyyy HH:mm";
  if (options && options.includeSeconds) timeFormat = "dd.MM.yyyy HH:mm:ss";
  return value ? format(ensureIsDate(value), timeFormat) : "";
}
function ensureIsDate(value) {
  return value instanceof Date ? value : new Date(value);
}
function toTimeString(value) {
  return value ? format(ensureIsDate(value), "HH:mm") : "";
}
function toDateString(value) {
  return value ? format(ensureIsDate(value), "dd.MM.yyyy") : "";
}
function getSelectedString() {
  return selected.value.length === 0
    ? ""
    : `${selected.value.length} record${
        selected.value.length > 1 ? "s" : ""
      } selected of ${rows.length}`;
}

function approvedByName(id) {
  const approvedByNameUser = userStore.users.find((u) => u.id === id);
  return (
    (approvedByNameUser?.firstName ? approvedByNameUser?.firstName : "") +
    " " +
    (approvedByNameUser?.lastName ? approvedByNameUser?.lastName : "")
  );
}

onMounted(async () => {
  await userStore.getUsers();
  await userStore.getUser();
  tableRef.value?.requestServerInteraction();
});
</script>
<style lang="scss" scoped>
.red-text {
  color: $red-4;
}
.green-text {
  color: $green-4;
}
.selection_width {
  width: 5%;
  float: left;
}
</style>
