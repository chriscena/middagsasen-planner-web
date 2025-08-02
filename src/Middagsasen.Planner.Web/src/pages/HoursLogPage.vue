<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Timeføring</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          title="Timeføring"
          dense
          flat
          round
          icon="more_time"
          @click="openTimetrackingForm"
        />
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
      <q-infinite-scroll :offset="100" @load="getUserWorkhours" :distance="100">
        <q-list role="list" separator>
          <q-item
            separator
            v-for="(hours, index) in userWorkHours"
            :key="index"
            clickable
            v-ripple
            @click="editWorkHour(hours)"
          >
            <q-item-section avatar>
              <q-icon
                name="check_circle"
                class="green-text"
                v-if="hours.approvalStatus === 1"
              />
              <q-icon
                name="cancel"
                class="red-text"
                v-if="hours.approvalStatus === 2"
              />
              <q-icon
                name="radio_button_unchecked"
                class="grey-text"
                v-if="!hours.approvalStatus"
              />
            </q-item-section>

            <q-item-section>
              <q-item-label
                >{{ toDateString(hours.startTime) }}
                {{ toTimeString(hours.startTime) }} -
                {{ toTimeString(hours.endTime) }}
              </q-item-label>
              <q-item-label caption>{{ hours.description }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-item-label>
                {{ hours.hours?.toFixed(1).toString().replace(".", ",") }} t
              </q-item-label></q-item-section
            >
          </q-item>
        </q-list>
        <q-inner-loading :showing="loading">
          <q-spinner size="3em" color="primary"></q-spinner>
        </q-inner-loading>
      </q-infinite-scroll>
    </div>
    <q-dialog v-model="showingTimetrackingForm" persistent>
      <TimeTrackingForm
        @cancel="showingTimetrackingForm = false"
        @saved="showingTimetrackingForm = false"
      ></TimeTrackingForm>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { useQuasar } from "quasar";
import { onMounted, ref, computed, useTemplateRef } from "vue";
import { Loading } from "quasar";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { format, formatISO } from "date-fns";
import { useRoute, useRouter } from "vue-router";
import TimeTrackingForm from "components/TimeTrackingForm.vue";

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
const hoverClass = ref(null);
const hoverClass2 = ref(null);
const confirmDeleteDialog = ref(false);
const showWorkHourDialog = ref(false);
const foundWorkHour = ref({});
const timerStarted = ref(false);
const loading = ref(false);
const userWorkHours = ref([]);
const currentWorkHour = ref({});
const currentPage = ref(1);
const currentUser = computed(() => authStore.user);
const userId = currentUser.value.id;
const activeWorkHour = ref({});
const workHour = ref({
  startTime: null,
  endTime: null,
  description: null,
});
const pagination = ref({
  rowsPerPage: 10,
  rowsNumber: undefined,
});
const dialogIcon = ref({
  class: "",
  name: "",
});

const noEdit = computed(() => {
  return foundWorkHour.value.approvalStatus !== null;
});

async function getUserWorkhours(index, done) {
  let stop = false;
  try {
    loading.value = true;
    const params = {
      page: index,
      pageSize: 20,
    };
    const response = await workHourStore.getWorkHoursByUser(userId, params);
    if (response.result.length > 0) {
      userWorkHours.value.push(...response.result);
    }
    stop =
      userWorkHours.value.length >= response.totalCount ||
      response.result.length === 0;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    loading.value = false;
    done(stop);
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

onMounted(async () => {
  await userStore.getUsers();
  await userStore.getUser();
});

function editWorkHour(hours) {
  currentWorkHour.value = { ...hours };
  showWorkHourDialog.value = true;
}

const showingTimetrackingForm = ref(false);
function openTimetrackingForm() {
  showingTimetrackingForm.value = true;
}
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
</style>
