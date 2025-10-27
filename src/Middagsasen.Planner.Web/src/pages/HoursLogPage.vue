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
      <q-list role="list" separator ref="workHoursList">
        <q-infinite-scroll
          :offset="100"
          @load="getUserWorkhours"
          :distance="100"
          ref="infiniteScroll"
          :scroll-target="workHoursList"
        >
          <q-item
            separator
            v-for="(hours, index) in viewModel.userWorkHours"
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
          <template #loading>
            <div class="row justify-center q-my-md">
              <q-spinner size="3em" color="primary"></q-spinner>
            </div>
          </template>
        </q-infinite-scroll>
      </q-list>
    </div>
    <q-dialog
      v-model="viewModel.showForm"
      persistent
      @hide="onTimeTrackingFormClosed"
    >
      <TimeTrackingForm
        :model-value="viewModel.selectedWorkHours"
        @cancel="viewModel.showForm = false"
        @saved="viewModel.showForm = false"
      ></TimeTrackingForm>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { useQuasar } from "quasar";
import { ref, computed, useTemplateRef, reactive } from "vue";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useAuthStore } from "src/stores/AuthStore";
import { format } from "date-fns";
import { useRouter } from "vue-router";
import TimeTrackingForm from "components/TimeTrackingForm.vue";

// store init
const $router = useRouter();
const workHourStore = useWorkHourStore();
const authStore = useAuthStore();
const $q = useQuasar();

// props and emits
const emit = defineEmits(["toggle-right", "toggle-left"]);

const viewModel = reactive({
  loading: false,
  userWorkHours: [],
  showForm: false,
  selectedWorkHours: null,
});

const infiniteScroll = useTemplateRef("infiniteScroll");

const currentUser = computed(() => authStore.user);
const userId = currentUser.value.id;

async function getUserWorkhours(index, done) {
  let stop = false;
  try {
    viewModel.loading = true;
    const params = {
      page: index,
      pageSize: 20,
    };
    const response = await workHourStore.getWorkHoursByUser(userId, params);
    if (response.result.length > 0) {
      viewModel.userWorkHours.push(...response.result);
    }
    stop =
      viewModel.userWorkHours.length >= response.totalCount ||
      response.result.length === 0;
  } catch (e) {
    console.error(e);
    stop = true;
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    viewModel.loading = false;
    done(stop);
  }
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

function editWorkHour(hours) {
  viewModel.selectedWorkHours = { ...hours };
  viewModel.showForm = true;
}

function onTimeTrackingFormClosed() {
  viewModel.selectedWorkHours = null;
  viewModel.userWorkHours = [];
  infiniteScroll.value.reset();
  infiniteScroll.value.resume();
}

function openTimetrackingForm() {
  viewModel.showForm = true;
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
