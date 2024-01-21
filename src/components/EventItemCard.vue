<template>
  <q-card
    v-for="resource in event.resources"
    :key="resource.id"
    :class="resourceClasses(timestamp, resource)"
    flat
  >
    <q-list role="list" separator>
      <q-item
        clickable
        dense
        class="q-py-sm text-bold"
        @click="showResourceInfo(resource)"
        ><q-item-section
          ><q-item-label lines="1"
            >{{ resource.resourceType.name }}
            <q-icon
              class="q-mr-xs"
              color="negative"
              name="warning"
              v-if="!(resource.minimumStaff <= resource.shifts.length)"
            ></q-icon
            ><q-icon
              v-if="resource.resourceType.files.length"
              color="blue-6"
              name="info"
              class="q-mr-xs"
            ></q-icon>
            <q-badge rounded color="yellow-8" v-if="resource.messages.length">
              {{ resource.messages.length }}</q-badge
            ></q-item-label
          > </q-item-section
        ><q-item-section side>{{
          formatStartEndTime(resource)
        }}</q-item-section></q-item
      >
      <q-item v-for="shift in createUserList(resource)" :key="shift.id">
        <q-item-section
          v-if="isAdmin || (isTrainer(resource.resourceType) && isTaken(shift))"
          avatar
        >
          <q-btn
            flat
            round
            icon="edit"
            size="sm"
            title="Endre vakt"
            @click="editShift(resource, shift)"
          ></q-btn>
        </q-item-section>
        <q-item-section>
          <q-item-label overline v-if="isVacant(shift)">Ledig</q-item-label>
          <q-item-label v-if="isTaken(shift)"
            ><q-icon
              v-if="shift.needsTraining"
              size="sm"
              name="escalator_warning"
            ></q-icon>
            {{ shift.user.fullName }}</q-item-label
          >
          <q-item-label caption v-if="isTaken(shift)">{{
            shift.comment
          }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-btn
            flat
            round
            icon="edit"
            title="Endre vakt"
            v-if="showEditButton(timestamp, shift)"
            @click="edit(shift, resource)"
          ></q-btn>
          <q-btn
            flat
            round
            :aria-label="`Ring til ${shift?.user?.fullName}`"
            icon="call"
            type="a"
            :href="'tel:' + shift?.user?.phoneNumber"
            v-if="showCallButton(shift)"
          ></q-btn>
          <q-btn
            flat
            round
            icon="add"
            :disable="adding"
            title="Ta vakt"
            v-if="showAddButton(timestamp, shift)"
            @click="checkTraining(resource)"
          ></q-btn>
        </q-item-section>
      </q-item>
      <q-item v-if="showAddAdditionalRow(timestamp, resource)">
        <q-item-section v-if="isAdmin" avatar>
          <q-btn
            flat
            round
            icon="edit"
            size="sm"
            title="Legge til vakt"
            @click="editShift(resource, shift)"
          ></q-btn>
        </q-item-section>
        <q-item-section> </q-item-section>
        <q-item-section side>
          <q-btn
            dense
            flat
            round
            icon="add"
            title="Ta vakt"
            @click="addUserAsResource(resource)"
          ></q-btn>
        </q-item-section>
      </q-item>
    </q-list>
  </q-card>

  <q-inner-loading :showing="adding">
    <q-spinner size="3em" color="primary"></q-spinner>
  </q-inner-loading>
  <q-dialog v-model="showingEdit" persistent>
    <q-card class="full-width">
      <q-card-section class="text-h6 row"
        ><span> Redigere</span><q-space></q-space
        ><q-btn
          size="md"
          flat
          dense
          round
          icon="delete"
          color="negative"
          @click="deleteShift"
        ></q-btn>
      </q-card-section>
      <q-card-section class="text-center" v-if="isMissingTrainingInfo()"
        >Vi har ikke registrert at du har fått opplæring til denne type vakter,
        trenger du det?</q-card-section
      >
      <q-card-section
        class="row text-center q-gutter-sm"
        v-if="isMissingTrainingInfo()"
        ><div class="col-12">
          <q-btn @click="setTraining(true)" unelevated color="primary" no-caps
            >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
          >
        </div>
        <div class="col-12">
          <q-btn flat no-caps @click="setTraining(false)"
            >Allerede fått opplæring, full kontroll! ✌️</q-btn
          >
        </div>

        <q-inner-loading :showing="savingTraining">
          <q-spinner size="3em" color="primary"></q-spinner> </q-inner-loading
      ></q-card-section>
      <q-card-section class="row q-gutter-sm">
        <q-input
          class="col-12"
          autofocus
          outlined
          label="Kommentar"
          v-model="selectedShift.comment"
        ></q-input>
        <div
          class="col-12 row items-right items-center"
          v-if="!isMissingTrainingInfo()"
        >
          <div class="q-mr-md">Fått opplæring</div>
          <q-btn-toggle
            disable
            v-model="selectedUserTraining.trainingComplete"
            toggle-color="primary"
            :options="[
              { label: 'Ja', value: true },
              { label: 'Nei', value: false },
            ]"
          /></div
      ></q-card-section>
      <q-card-actions align="right">
        <q-btn flat label="Avbryt" no-caps @click="showingEdit = false"></q-btn>
        <q-btn
          unelevated
          color="primary"
          label="Lagre"
          no-caps
          @click="updateShift"
        ></q-btn> </q-card-actions
      ><q-inner-loading :showing="saving">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showingAdminEdit" persistent>
    <q-card class="full-width">
      <q-card-section class="text-h6 row"
        ><span> Endre vakt</span><q-space></q-space
        ><q-btn
          :disable="!isAdmin"
          size="md"
          flat
          dense
          round
          icon="delete"
          color="negative"
          @click="deleteShift"
          v-if="selectedShift.id"
        ></q-btn>
      </q-card-section>
      <q-card-section class="q-gutter-sm">
        <q-select
          :disable="!isAdmin"
          label="Navn"
          autofocus
          outlined
          :options="users"
          :loading="loadingUsers"
          option-label="fullName"
          option-value="id"
          v-model="selectedShift.user"
          @update:model-value="onUserUpdated"
        >
          <template v-slot:option="scope">
            <q-item v-bind="scope.itemProps">
              <q-item-section>
                {{ scope.opt.fullName }}
              </q-item-section>
              <q-item-section side>
                <q-item-label caption>{{ scope.opt.phoneNo }}</q-item-label>
              </q-item-section>
            </q-item>
          </template>
        </q-select>
        <q-input
          :disable="!isAdmin"
          outlined
          label="Kommentar"
          v-model="selectedShift.comment"
        ></q-input>
        <div
          class="row items-right items-center"
          v-if="selectedResource.resourceType?.hasTraining"
        >
          <div class="q-mr-md">Fått opplæring</div>
          <q-btn-toggle
            v-model="selectedUserTraining.trainingComplete"
            :disable="!selectedShift.user"
            toggle-color="primary"
            :options="[
              { label: 'Ja', value: true },
              { label: 'Nei', value: false },
            ]"
          /></div
      ></q-card-section>
      <q-card-actions align="right">
        <q-btn
          flat
          label="Avbryt"
          no-caps
          @click="showingAdminEdit = false"
        ></q-btn>
        <q-btn
          unelevated
          color="primary"
          label="Lagre"
          no-caps
          @click="updateShift"
          :disable="!selectedShift.user"
        ></q-btn> </q-card-actions
      ><q-inner-loading :showing="saving">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showingTrainingDialog" persistent>
    <q-card class="text-center">
      <q-card-section
        >Vi har ikke registrert at du har fått opplæring til denne type vakter,
        trenger du det?</q-card-section
      >
      <q-card-section class="q-gutter-md"
        ><q-btn
          @click="addUserAsResourceWithTraining"
          unelevated
          color="primary"
          no-caps
          >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
        ></q-card-section
      >
      <q-card-section
        ><q-btn flat no-caps @click="addUserAsResourceWithoutTraining"
          >Allerede fått opplæring, full kontroll! ✌️</q-btn
        ></q-card-section
      >
    </q-card>
  </q-dialog>

  <q-dialog v-model="showingResourceInfo" :maximized="$q.platform.is.mobile">
    <q-card
      :style="
        $q.platform.is.desktop
          ? 'max-width: 600px;min-width: 400px;width: 80vw;height: 80vw;'
          : ''
      "
    >
      <q-card-section class="row">
        <q-btn
          flat
          dense
          round
          icon="close"
          @click="showingResourceInfo = false"
          title="Lukk"
        ></q-btn>
        <div class="text-h6">
          {{ selectedResource.resourceType.name }}
        </div>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section>
        <q-card
          flat
          bordered
          v-if="selectedResource.resourceType.files?.length"
        >
          <q-card-section class="q-py-sm text-subtitle2">
            Nyttig info
          </q-card-section>
          <q-separator></q-separator>
          <q-list>
            <q-item
              v-for="file in selectedResource.resourceType.files"
              :key="file.id"
              clickable
              :href="`/api/resourcetypes/${file.resourceTypeId}/files/${file.id}`"
            >
              <q-item-section
                ><q-item-label lines="1">{{
                  file.description
                }}</q-item-label></q-item-section
              >
              <q-item-section side
                ><q-icon name="download"></q-icon> </q-item-section
            ></q-item> </q-list></q-card
      ></q-card-section>
      <q-card-section>
        <q-card flat class="bg-yellow-2">
          <q-card-section class="q-py-sm text-subtitle2">
            Beskjed til vakta</q-card-section
          >
          <q-separator></q-separator>
          <q-list separator>
            <q-item
              v-for="message in selectedResource.messages"
              :key="message.id"
            >
              <q-item-section>
                <q-item-label overline>{{
                  format(parseISO(message.created), "EEE d. LLL")
                }}</q-item-label>
                <q-item-label class="q-py-sm">{{
                  message.message
                }}</q-item-label>
                <q-item-label caption>{{
                  message.createdBy.fullName
                }}</q-item-label>
              </q-item-section>

              <q-item-section side top>
                <q-btn
                  flat
                  round
                  size="sm"
                  icon="delete"
                  @click="deleteMessage(message)"
                  :disable="!canDeleteMessage(message)"
                  :loading="deletingMessage"
                ></q-btn>
              </q-item-section>
            </q-item>
          </q-list>
          <q-card-section>
            <q-input
              outlined
              label="Ny beskjed"
              autogrow
              type="textarea"
              v-model="newMessage"
            ></q-input>
          </q-card-section>
          <q-card-actions
            ><q-btn
              label="Nullstill"
              @click="newMessage = null"
              no-caps
              flat
              :disable="savingMessage"
            ></q-btn
            ><q-space></q-space>
            <q-btn
              label="Lagre"
              @click="saveMessage"
              color="primary"
              no-caps
              unelevated
              :loading="savingMessage"
            ></q-btn>
          </q-card-actions>
        </q-card>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { computed, ref } from "vue";
import { useQuasar } from "quasar";
import { today } from "@quasar/quasar-ui-qcalendar";
import { parseISO, format } from "date-fns";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { useAuthStore } from "stores/AuthStore";

const props = defineProps({
  modelValue: { type: Object, require: true },
  isAdmin: { type: Boolean, default: false },
  timestamp: { type: Object, require: true },
});

const $q = useQuasar();
const isAdmin = computed(() => props.isAdmin);

const currentUser = computed(() => authStore.user);
const currentUserTrainings = computed(() =>
  currentUser.value.trainings.map((t) => t.resourceTypeId)
);
const eventStore = useEventStore();
const authStore = useAuthStore();
const userStore = useUserStore();

function createUserList(resource) {
  const list = [];
  list.push(...resource.shifts);
  const neededStaff = resource.minimumStaff - list.length;

  if (neededStaff > 0) {
    for (let i = 0; i < neededStaff; i += 1) {
      list.push({
        id: 0,
        user: null,
        comment: null,
      });
    }
  }
  return list;
}

const event = computed(() => props.modelValue);

function formatTime(isoDateTime) {
  if (!isoDateTime) return null;
  const date = parseISO(isoDateTime);
  return format(date, "HH:mm");
}

function formatStartEndTime(event) {
  return `${formatTime(event.startTime)}-${formatTime(event.endTime)}`;
}
function resourceClasses(timestamp, resource) {
  return (
    "q-mt-sm q-mx-sm " +
    (timestamp.date < today()
      ? "bg-grey-3"
      : resource.minimumStaff <= resource.shifts.length
      ? "bg-green-1"
      : "bg-red-1")
  );
}

function isVacant(shift) {
  return (shift?.user?.id ?? 0) === 0 ?? false;
}

function isTaken(shift) {
  return shift?.user?.id > 0 ?? false;
}

function showEditButton(timestamp, shift) {
  return (
    timestamp.date >= today() &&
    (shift?.user?.id ?? 0) === currentUser.value?.id
  );
}

function showCallButton(shift) {
  return (
    (shift?.user?.id ?? 0) > 0 &&
    (shift?.user?.id ?? 0) !== currentUser.value?.id
  );
}
function showAddButton(timestamp, shift) {
  return timestamp.date >= today() && (shift?.user?.id ?? 0) === 0;
}

function showAddAdditionalRow(timestamp, resource) {
  return (
    (isTrainer(resource.resourceType) || isAdmin.value) &&
    timestamp.date >= today() &&
    resource.shifts.length >= resource.minimumStaff
  );
}

const adding = ref(false);
const showingTrainingDialog = ref(false);
async function checkTraining(resource) {
  try {
    adding.value = true;
    selectedResource.value = resource;
    if (isMissingTrainingInfo(resource)) {
      showingTrainingDialog.value = true;
    } else {
      const resourceType =
        resource?.resourceType ?? selectedResource.value?.resourceType;
      if (resourceType.hasTraining) {
        const training = currentUser.value?.trainings?.find(
          (t) => t.resourceTypeId === resourceType.id
        );
        await addUserAsResource(resource, training);
      } else {
        await addUserAsResource(resource);
      }
    }
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da du skulle ta vakta! 🙈",
    });
  } finally {
    adding.value = false;
  }
}

function isMissingTrainingInfo(resource) {
  const resourceType =
    resource?.resourceType ?? selectedResource.value?.resourceType;
  return (
    resourceType.hasTraining &&
    !currentUserTrainings.value.includes(resourceType.id)
  );
}

const savingTraining = ref(false);
async function setTraining(needTraining) {
  try {
    savingTraining.value = true;
    await eventStore.addTraining(
      selectedResource.value,
      currentUser.value,
      needTraining
    );
  } catch (error) {
  } finally {
    savingTraining.value = false;
  }
}

function onUserUpdated() {
  selectedShift.value.userId = selectedShift.value?.user?.id ?? 0;
  const resourceTypeId = selectedResource.value?.resourceType.id;
  const training = selectedShift.value?.user.trainings.find(
    (t) => t.resourceTypeId === resourceTypeId
  );
  selectedUserTraining.value = training ?? {
    id: 0,
    resourceTypeId: resourceTypeId,
    userId: selectedShift.value.userId,
    trainingComplete: null,
  };
}

async function addUserAsResourceWithTraining() {
  showingTrainingDialog.value = false;
  let training = emptyUserTraining();
  training.trainingComplete = false;
  await addUserAsResource(selectedResource.value, training);
}

async function addUserAsResourceWithoutTraining() {
  showingTrainingDialog.value = false;
  let training = emptyUserTraining();
  training.trainingComplete = true;
  await addUserAsResource(selectedResource.value, training);
}

async function addUserAsResource(resource, training) {
  try {
    adding.value = true;

    await eventStore.addShift(resource, currentUser.value, null, training);
    $q.notify({
      message: "Woohoo! Du har tatt en vakt 🎉",
    });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da du skulle ta vakta! 🙈",
    });
  } finally {
    adding.value = false;
  }
}

const showingEdit = ref(false);
const selectedShift = ref(null);
const emptyUserTraining = () => {
  return {
    id: 0,
    resourceTypeId: 0,
    userId: 0,
    trainingComplete: null,
  };
};
const selectedUserTraining = ref(emptyUserTraining());
function edit(shift, resource) {
  selectedResource.value = resource;
  selectedShift.value = Object.assign({}, shift);
  const resourceTypeId = resource?.resourceType.id;
  const training =
    selectedShift.value?.user.trainings.find(
      (t) => t.resourceTypeId === resourceTypeId
    ) ?? emptyUserTraining();
  selectedUserTraining.value = training;
  showingEdit.value = true;
}

const saving = ref(false);
async function updateShift() {
  try {
    saving.value = true;
    if (isAdmin.value && selectedShift.value.id === 0) {
      await eventStore.addShift(
        selectedResource.value,
        selectedShift.value.user,
        selectedShift.value.comment,
        selectedUserTraining.value
      );
    } else {
      await eventStore.updateShift(
        selectedResource.value,
        selectedShift.value,
        selectedUserTraining.value
      );
    }

    selectedResource.value = null;
    selectedShift.value = null;
    selectedUserTraining.value = null;
    showingEdit.value = false;
    showingAdminEdit.value = false;
    $q.notify({
      message: "Endringer er lagret 👍",
    });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da vi skulle lagre endringene! 🙈",
    });
  } finally {
    saving.value = false;
  }
}

async function deleteShift() {
  try {
    saving.value = true;
    await eventStore.deleteShift(selectedShift.value);
    showingEdit.value = false;
    showingAdminEdit.value = false;
    $q.notify({
      message: "Ajaj! Du har tatt bort vakta 😱",
    });
  } catch (error) {
    $q.notify({
      message: "Oh no! Noe tryna da vi skulle ta bort vakta... 🙈",
    });
  } finally {
    saving.value = false;
  }
}

function isTrainer(resourceType) {
  const trainerIds = resourceType.trainers.map((t) => t.id);
  return trainerIds.includes(currentUser.value.id);
}

const selectedResource = ref(null);
const showingAdminEdit = ref(false);
async function editShift(resource, shift) {
  selectedShift.value = Object.assign({ userId: 0 }, shift);
  selectedResource.value = resource;
  const resourceTypeId = resource?.resourceType.id;
  const training =
    selectedShift.value?.user?.trainings.find(
      (t) => t.resourceTypeId === resourceTypeId
    ) ?? emptyUserTraining();
  selectedUserTraining.value = training;
  showingAdminEdit.value = true;
  if (isAdmin.value) await getUsers();
}

const loadingUsers = ref(false);
const users = computed(() => userStore.users);
async function getUsers() {
  try {
    loadingUsers.value = true;
    await userStore.getUsers();
  } catch (error) {
    $q.notify({ message: "Klarte ikke å hente lista over brukere." });
  } finally {
    loadingUsers.value = false;
  }
}

const showingResourceInfo = ref(false);
function showResourceInfo(resource) {
  selectedResource.value = resource;
  showingResourceInfo.value = true;
}

const newMessage = ref(null);
const savingMessage = ref(false);
async function saveMessage() {
  try {
    savingMessage.value = true;
    const model = {
      message: newMessage.value,
    };
    const response = await eventStore.addMessage(
      selectedResource.value.id,
      model
    );
    selectedResource.value.messages.push(response);
    newMessage.value = null;
    $q.notify({ message: "Beskjeden er lagret. 📨" });
  } catch (error) {
    $q.notify({ message: "Klarte ikke å lagre beskjed. 😿" });
    console.log(error);
  } finally {
    savingMessage.value = false;
  }
}

const deletingMessage = ref(false);
async function deleteMessage(message) {
  try {
    deletingMessage.value = true;
    await eventStore.deleteMessage(message);
    selectedResource.value.messages = selectedResource.value.messages.filter(
      (m) => m.id !== message.id
    );
    newMessage.value = null;
    $q.notify({ message: "Beskjeden er slettet. 📤" });
  } catch (error) {
    $q.notify({ message: "Klarte ikke å slette beskjed. 😿" });
    console.log(error);
  } finally {
    deletingMessage.value = false;
  }
}

function canDeleteMessage(message) {
  return isAdmin.value || message.createdBy.id === currentUser.value.id;
}
</script>
