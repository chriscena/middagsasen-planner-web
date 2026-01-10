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
        <q-toolbar-title>Time-godkjenning</q-toolbar-title>
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
        :grid="$q.screen.lt.md"
        :bordered="$q.screen.lt.md"
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
        selection="multiple"
        v-model:selected="selectedWorkHours"
        :filter="filter"
        @row-click="(_, row) => openWorkHours(row)"
      >
        <template #top>
          <div class="row q-gutter-sm q-pr-md">
            <q-radio
              dense
              :disable="loading"
              :model-value="approvedFilter"
              label="Ubehandlet"
              :val="3"
              @update:model-value="(val) => setFilter({ approved: val })"
              ><q-badge class="q-ml-xs" v-show="pendingHours > 0"
                >{{ formatNumber(pendingHours) }} t</q-badge
              ></q-radio
            >
            <q-radio
              dense
              :disable="loading"
              :model-value="approvedFilter"
              label="Godkjent"
              :val="1"
              @update:model-value="(val) => setFilter({ approved: val })"
              ><q-badge
                color="positive"
                class="q-ml-xs"
                v-show="approvedHours > 0"
                >{{ formatNumber(approvedHours) }} t</q-badge
              ></q-radio
            >
            <q-radio
              dense
              :disable="loading"
              :model-value="approvedFilter"
              label="Avslått"
              :val="2"
              @update:model-value="(val) => setFilter({ approved: val })"
              ><q-badge
                color="warning"
                class="q-ml-xs"
                v-show="rejectedHours > 0"
                >{{ formatNumber(rejectedHours) }} t</q-badge
              ></q-radio
            >
          </div>
          <q-space></q-space>
          <span class="q-pa-md row">
            <span class="q-pa-sm">
              <q-btn
                v-if="isAdmin"
                :disable="!(selectedWorkHours.length > 0)"
                label="Godkjenn"
                :size="$q.screen.gt.sm ? 'large' : 'medium'"
                @click="
                  showApprovalDialog = true;
                  approvalType = 1;
                "
                color="primary"
              />
            </span>
            <span class="q-pa-sm">
              <q-btn
                v-if="isAdmin"
                :disable="!(selectedWorkHours.length > 0)"
                label="Avslå"
                :size="$q.screen.gt.sm ? 'large' : 'medium'"
                @click="
                  showApprovalDialog = true;
                  approvalType = 2;
                "
                color="primary"
              />
            </span>
          </span>
          <q-card v-if="$q.screen.lt.md && approvedFilter === 3" class="col-12">
            <q-card-section>
              <q-checkbox
                v-model="selectAllBox"
                label="Velg alle"
                @update:model-value="toggleSelectAll"
              >
              </q-checkbox>
            </q-card-section>
          </q-card>
        </template>
        <template #item="props" v-if="$q.screen.lt.md">
          <q-card
            :props="props"
            class="q-pa-sm q-my-sm col-12 grid-style-transition"
            :style="props.selected ? 'transform: scale(0.95);' : ''"
          >
            <q-card-section>
              <div class="row">
                <q-icon
                  size="lg"
                  name="check_circle"
                  class="green-text q-pr-lg"
                  v-if="props.row.approvalStatus === 1"
                />
                <q-icon
                  size="lg"
                  name="cancel"
                  class="red-text q-pr-lg"
                  v-if="props.row.approvalStatus === 2"
                />
                <q-checkbox
                  v-if="props.row.approvalStatus === null"
                  v-model="props.selected"
                  class="q-pr-md"
                />
                <div class="text-h6 q-pr-lg">
                  <q-item-label caption>
                    <span>
                      <span> {{ toDateString(props.row.startTime) }} </span>
                      <span>
                        {{ toTimeString(props.row.startTime) }} -
                        {{ toTimeString(props.row.endTime) }}
                      </span>
                    </span>
                  </q-item-label>
                  {{ userNameById(props.row.userId) }}
                </div>
                <q-space></q-space>
                <q-item-label caption class="q-pt-md">
                  {{ formatNumber(props.row.hours) }}
                  t
                </q-item-label>
              </div>
            </q-card-section>
            <q-separator></q-separator>
            <q-card-section>
              <q-item-label
                style="font-size: 14px"
                :caption="!props.row.description"
              >
                {{ props.row.description ?? "Ingen beskrivelse..." }}
              </q-item-label>
            </q-card-section>
            <q-separator></q-separator>
            <q-card-section class="row">
              <q-space></q-space>
              <q-item-label caption>
                {{
                  props.row.approvedBy
                    ? props.row.approvalStatus !== null
                      ? props.row.approvalStatus === 1
                        ? "Godkjent av: " + userNameById(props.row.approvedBy)
                        : "Avslått av: " + userNameById(props.row.approvedBy)
                      : ""
                    : ""
                }}
              </q-item-label>
            </q-card-section>
          </q-card>
        </template>
        <template #header-selection="props" v-if="$q.screen.gt.md">
          <q-checkbox
            v-if="isAdmin && approvedFilter === 3"
            :props="props"
            v-model="props.selected"
          />
        </template>
        <template #body-selection="props">
          <q-checkbox
            v-if="!props.row.approvalStatus && isAdmin"
            :props="props"
            v-model="props.selected"
          />
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <q-icon
              size="md"
              name="check_circle"
              class="green-text"
              v-if="props.row.approvalStatus === 1"
            />
            <q-icon
              size="md"
              name="cancel"
              class="red-text"
              v-if="props.row.approvalStatus === 2"
            />
          </q-td>
        </template>
        <template #body-cell-from="props">
          <q-td :props="props">
            {{ toDateString(props.row.startTime) }}
            {{ toTimeString(props.row.startTime) }}
          </q-td>
        </template>
        <template #body-cell-to="props">
          <q-td :props="props">
            {{ toDateString(props.row.endTime) }}
            {{ toTimeString(props.row.endTime) }}
          </q-td>
        </template>
        <template #body-cell-hours="props">
          <q-td :props="props">
            {{ formatNumber(props.row.hours) }}
            t
          </q-td>
        </template>
      </q-table>
    </div>
  </q-page>
  <q-dialog v-model="showApprovalDialog">
    <q-card>
      <q-card-section class="text-h6">
        Bekreft {{ approvalType == 1 ? "godkjenning" : "avslag" }}
      </q-card-section>
      <q-card-section>
        <div>
          Er du sikker på at du vil sette {{ selectedWorkHours.length }}
          {{ selectedWorkHours.length > 1 ? "timeføringer" : "timeføring" }}
          til
          <span
            :class="
              approvalType == 1 ? 'green-text text-bold ' : 'red-text text-bold'
            "
            >{{ approvalType == 1 ? "godkjent" : "avslått" }}</span
          >?
        </div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn
          v-if="isAdmin"
          :disable="!(selectedWorkHours.length > 0)"
          label="Avbryt"
          no-caps
          flat
          @click="showApprovalDialog = false"
        />
        <q-btn
          v-if="isAdmin"
          :disable="!(selectedWorkHours.length > 0)"
          label="Lagre"
          no-caps
          @click="approveUpdateRows(approvalType)"
          color="primary"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showWorkHourDialog">
    <q-card class="q-pa-sm" style="width: 100%">
      <q-card-section>
        <div class="row">
          <q-btn-dropdown
            size="lg"
            :icon="dialogIcon.name"
            :class="dialogIcon.class"
            class="q-pa-xs q-pl-sm"
          >
            <q-list>
              <q-item
                v-if="foundWorkHour.approvalStatus !== 1"
                clickable
                @click="changeStatus(foundWorkHour.workHourId, 1)"
              >
                <q-item-section>
                  <div>
                    <q-icon
                      name="check_circle"
                      class="green-text q-pr-sm"
                      size="md"
                    />Godkjent
                  </div>
                </q-item-section>
              </q-item>
              <q-item
                v-if="foundWorkHour.approvalStatus !== 2"
                clickable
                @click="changeStatus(foundWorkHour.workHourId, 2)"
              >
                <q-item-section>
                  <div>
                    <q-icon name="cancel" class="red-text q-pr-sm" size="md" />
                    Avslått
                  </div>
                </q-item-section>
              </q-item>
              <q-item
                v-if="foundWorkHour.approvalStatus !== null"
                clickable
                @click="changeStatus(foundWorkHour.workHourId, null)"
              >
                <q-item-section>
                  <div>
                    <q-icon
                      size="md"
                      class="q-pr-sm grey-text"
                      name="radio_button_unchecked"
                    />
                    Ingen status
                  </div>
                </q-item-section>
              </q-item>
            </q-list>
          </q-btn-dropdown>
          <div class="text-h6 q-px-lg q-pt-xs">
            <q-item-label caption>
              <span
                v-if="
                  toDateString(foundWorkHour.startTime) ==
                  toDateString(foundWorkHour.endTime)
                "
              >
                <span> {{ toDateString(foundWorkHour.startTime) }} | </span>
                <span>
                  {{ toTimeString(foundWorkHour.startTime) }} -
                  {{ toTimeString(foundWorkHour.endTime) }}
                </span>
              </span>
              <span
                v-if="
                  toDateString(foundWorkHour.startTime) !=
                  toDateString(foundWorkHour.endTime)
                "
              >
                <div>
                  Fra: {{ toDateString(foundWorkHour.startTime) }} |
                  {{ toTimeString(foundWorkHour.startTime) }}
                </div>
                <div>
                  Til: {{ toDateString(foundWorkHour.endTime) }} |
                  {{ toTimeString(foundWorkHour.endTime) }}
                </div>
              </span>
            </q-item-label>
            {{ userNameById(foundWorkHour.userId) }}
          </div>
          <q-space></q-space>
          <q-item-label caption class="q-pt-md">
            {{ formatNumber(foundWorkHour.hours) }}
            t
          </q-item-label>
        </div>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section>
        <q-item-label
          style="font-size: medium"
          :caption="!foundWorkHour.description"
        >
          {{ foundWorkHour.description ?? "Ingen beskrivelse..." }}
        </q-item-label>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section class="row">
        <q-space></q-space>
        <q-item-label caption>
          {{
            foundWorkHour.approvedBy
              ? foundWorkHour.approvalStatus !== null
                ? foundWorkHour.approvalStatus === 1
                  ? "Godkjent av: " + userNameById(foundWorkHour.approvedBy)
                  : "Avslått av: " + userNameById(foundWorkHour.approvedBy)
                : ""
              : ""
          }}
        </q-item-label>
      </q-card-section>
      <q-card-section class="q-px-xl row justify-center">
        <q-btn label="Lukk" size="large" @click="showWorkHourDialog = false" />
      </q-card-section>
    </q-card>
  </q-dialog>
</template>
<script setup>
import { useQuasar } from "quasar";
import { onMounted, ref, computed, useTemplateRef } from "vue";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { format } from "date-fns";
import { useRoute, useRouter } from "vue-router";
import { formatNumber } from "src/shared/formatter.js";

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
const selectAllBox = ref(false);
const approvedHours = ref(0);
const pendingHours = ref(0);
const rejectedHours = ref(0);
const foundWorkHour = ref({});
const showWorkHourDialog = ref(false);
const selectedWorkHours = ref([]);
const tableRef = useTemplateRef("tableRef");
const loading = ref(false);
const showApprovalDialog = ref(false);
const userWorkHours = ref([]);
const currentPage = ref(1);
const currentUser = computed(() => authStore.user);
const currentUserId = currentUser.value.id;
const pagination = ref({
  rowsPerPage: Number.isInteger(parseInt($route.query.rowPP))
    ? parseInt($route.query.rowPP)
    : 15,
  page: Number.isInteger(parseInt($route.query.page))
    ? parseInt($route.query.page)
    : 1,
  rowsNumber: undefined,
});
const dialogIcon = ref({
  class: "",
  name: "",
});

// constants
const columns = [
  {
    name: "status",
    label: "Status",
    field: (row) => row.approvalStatus,
    align: "left",
    headerStyle: "width: 10%",
    style: "width: 10%",
  },
  {
    name: "user",
    label: "Bruker",
    field: (row) => row.userId,
    format: (val) => userNameById(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "approvedBy",
    label: "Godkjent av",
    field: (row) => row.approvedBy,
    format: (val) => userNameById(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "description",
    label: "Beskrivelse",
    field: (row) => row.description,
    align: "left",
    headerStyle: "width: 50%",
    style:
      "width: 50%; max-width: 100px; text-overflow: ellipsis; overflow: hidden;",
  },
  {
    name: "from",
    label: "Fra",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "to",
    label: "Til",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "hours",
    label: "Timer",
    field: (row) => row.hours,
    format: (val) => formatNumber(val),
    align: "right",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
];

// computed
const visibleColumns = computed(() => {
  let cols = [];
  cols.push("user");
  if ($q.screen.gt.xs && approvedFilter.value !== 3) cols.push("status");
  if ($q.screen.gt.xs && approvedFilter.value !== 3) cols.push("approvedBy");
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
  return $route.query.a !== undefined ? parseInt($route.query.a) : 3;
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

    const [response, sumResponse] = await Promise.all([
      workHourStore.getWorkHours(params),
      workHourStore.getWorkHoursSums(),
    ]);

    console.log("userWorkHours", response);
    userWorkHours.value = response.result;
    pagination.value.rowsNumber = workHourStore.userWorkHours.totalCount;
    pagination.value.page = props.pagination.page;
    pagination.value.rowsPerPage = props.pagination.rowsPerPage;

    approvedHours.value = sumResponse.approvedHours;
    pendingHours.value = sumResponse.pendingHours;
    rejectedHours.value = sumResponse.rejectedHours;
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

async function approveUpdateRows(status) {
  try {
    loading.value = true;
    for (let i = 0; i < selectedWorkHours.value.length; i++) {
      try {
        const model = {
          approvedBy: currentUserId,
          approvalStatus: status,
          workHourId: selectedWorkHours.value[i].workHourId,
        };
        await workHourStore.updateApproval(model);
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
    showApprovalDialog.value = false;
    tableRef.value?.requestServerInteraction();
  }
}

async function changeStatus(workHourId, status) {
  try {
    loading.value = true;
    const model = {
      approvedBy: status === null ? null : currentUserId,
      approvalStatus: status,
      workHourId: workHourId,
    };
    await workHourStore.updateApproval(model);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    loading.value = false;
    showWorkHourDialog.value = false;
    tableRef.value?.requestServerInteraction();
  }
}

async function openWorkHours(workHourRow) {
  foundWorkHour.value = userWorkHours.value?.find(
    (w) => w.workHourId === workHourRow.workHourId
  );
  if (!foundWorkHour.value) {
    return;
  }
  if (foundWorkHour.value.approvalStatus === 1) {
    dialogIcon.value.class = "green-text";
    dialogIcon.value.name = "check_circle";
  } else if (foundWorkHour.value.approvalStatus === 2) {
    dialogIcon.value.class = "red-text";
    dialogIcon.value.name = "cancel";
  } else {
    dialogIcon.value.class = "grey-text";
    dialogIcon.value.name = "radio_button_unchecked";
  }
  showWorkHourDialog.value = true;
}

function toggleSelectAll(val) {
  if (val) {
    selectedWorkHours.value = [...userWorkHours.value];
  } else {
    selectedWorkHours.value = [];
  }
}

async function setFilter(filter) {
  if ((filter.approved || approvedFilter.value) !== 3) {
    selectedWorkHours.value = [];
  }
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

function userNameById(id) {
  const approvedByNameUser = userStore.users.find((u) => u.id === id);
  return approvedByNameUser?.fullName ? approvedByNameUser?.fullName : "";
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
.orange-text {
  color: $orange-4;
}
.grey-text {
  color: $grey-7;
}
.selection_width {
  width: 5%;
}
.grid-style-transition {
  transition: transform 0.28s, background-color 0.28s;
}
</style>
