<template>
  <q-page padding
    ><q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Brukere</q-toolbar-title>
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
    <q-input
      outlined
      clearable
      v-model="filter"
      debounce="500"
      placeholder="Søk"
      class="q-mb-sm"
    >
      <template v-slot:prepend>
        <q-icon name="search"></q-icon>
      </template>
    </q-input>

    <q-list role="list" separator>
      <q-item
        separator
        v-for="user in users"
        :key="user.id"
        clickable
        @click="editUser(user)"
        v-ripple
        title="Endre bruker"
      >
        <q-item-section>
          <q-item-label lines="1"
            >{{ user.fullName }}
            <q-icon v-if="user.isAdmin" color="primary" name="shield"></q-icon
          ></q-item-label>
          <q-item-label caption lines="1">{{ user.phoneNo }}</q-item-label>
        </q-item-section>
        <q-item-section side v-if="getApprovedHours(user.id) > 0">
          <q-item-label class="text-weight-medium">{{
            formatHours(getApprovedHours(user.id))
          }}</q-item-label>
        </q-item-section>
      </q-item>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner> </q-inner-loading
    ><q-footer>
      <q-toolbar>
        <q-space></q-space>
        <q-btn
          fab
          unelevated
          padding="md"
          class="q-ma-xs"
          icon="add"
          color="accent"
          text-color="blue-grey-9"
          @click="newUser"
          title="Legg til ny bruker"
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEditDialog" persistent>
      <q-card class="full-width">
        <q-card-section class="row"
          ><span class="text-h6">{{
            selectedUser.id ? "Endre bruker" : "Legg til bruker"
          }}</span
          ><q-space></q-space
          ><q-btn
            v-if="selectedUser.id"
            flat
            round
            dense
            icon="delete"
            @click="deleteUser"
            color="negative"
          ></q-btn
        ></q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input
            outlined
            autofocus
            v-model="selectedUser.phoneNo"
            label="Mobiltelefon"
          ></q-input>
          <q-input
            outlined
            v-model="selectedUser.firstName"
            label="Fornavn"
          ></q-input>
          <q-input
            outlined
            v-model="selectedUser.lastName"
            label="Etternavn"
          ></q-input>
          <q-checkbox
            label="Administrator"
            v-model="selectedUser.isAdmin"
            :disable="currentUser?.id === selectedUser.id"
          ></q-checkbox
          ><q-checkbox
            label="Skjul fra telefonlista"
            v-model="selectedUser.isHidden"
          ></q-checkbox>
        </q-card-section>

        <!-- Competency section (only for existing users) -->
        <q-card-section v-if="selectedUser.id">
          <div class="text-subtitle2 q-mb-sm">Kompetanser</div>
          <q-spinner
            v-if="loadingUserCompetencies"
            size="1.5em"
            color="primary"
            class="q-mb-sm"
          ></q-spinner>
          <q-list dense separator v-if="editUserCompetencies.length > 0">
            <q-item v-for="uc in editUserCompetencies" :key="uc.id">
              <q-item-section>
                <q-item-label>{{ uc.competencyName }}</q-item-label>
                <q-item-label caption v-if="uc.expiryDate">
                  Utløper: {{ formatDate(uc.expiryDate) }}
                </q-item-label>
              </q-item-section>
              <q-item-section side>
                <div class="row items-center q-gutter-xs">
                  <q-badge
                    v-if="uc.approved && !uc.isExpired"
                    color="green"
                    label="Godkjent"
                  ></q-badge>
                  <q-badge
                    v-else-if="uc.approved && uc.isExpired"
                    color="red"
                    label="Utløpt"
                  ></q-badge>
                  <q-badge
                    v-else
                    color="orange"
                    label="Venter på godkjenning"
                  ></q-badge>
                  <q-btn
                    v-if="!uc.approved"
                    flat
                    dense
                    no-caps
                    color="positive"
                    label="Godkjenn"
                    :loading="approvingId === uc.id"
                    @click="approveCompetency(uc)"
                  ></q-btn>
                  <q-btn
                    v-if="uc.approved"
                    flat
                    dense
                    no-caps
                    color="negative"
                    label="Trekk tilbake"
                    :loading="revokingId === uc.id"
                    @click="revokeCompetency(uc)"
                  ></q-btn>
                </div>
              </q-item-section>
            </q-item>
          </q-list>
          <div
            v-else-if="!loadingUserCompetencies"
            class="text-caption text-grey q-mb-sm"
          >
            Ingen kompetanser registrert
          </div>
          <div class="row items-center q-gutter-sm q-mt-sm">
            <q-select
              class="col"
              v-model="adminSelectedCompetencyId"
              :options="adminAvailableCompetencies"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              outlined
              dense
              label="Legg til kompetanse"
            ></q-select>
            <q-btn
              flat
              round
              dense
              icon="add"
              color="primary"
              :disable="!adminSelectedCompetencyId"
              :loading="adminAddingCompetency"
              @click="adminAddCompetency"
            ></q-btn>
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn
            label="Avbryt"
            no-caps
            flat
            @click="showingEditDialog = false"
          ></q-btn>
          <q-btn
            label="Lagre"
            @click="saveUser"
            no-caps
            unelevated
            color="primary"
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useUserStore } from "stores/UserStore";
import { useAuthStore } from "stores/AuthStore";
import { useCompetencyStore } from "stores/CompetencyStore";
import { formatHours } from "src/shared/formatter";
import { getApiErrorMessage } from "src/shared/apiError";
import { formatDate } from "src/shared/time";
import type { UserCompetencyResponse, UserResponse } from "src/types";

// Skjemaet i redigeringsdialogen. Ved redigering er det en kopi av
// UserResponse (inkl. øvrige felt), ved ny bruker er id null.
interface UserForm {
  id: number | null;
  phoneNo: string | null;
  firstName?: string | null;
  lastName?: string | null;
  isAdmin: boolean;
  isHidden: boolean;
}

const emit = defineEmits<{ "toggle-right": [] }>();
const loading = ref(false);
const $q = useQuasar();
const userStore = useUserStore();
const authStore = useAuthStore();
const competencyStore = useCompetencyStore();

const filter = ref<string | null>(null);

const currentUser = computed(() => authStore.user);

const users = computed((): UserResponse[] => {
  const filterValue = filter.value;
  return filterValue
    ? userStore.users.filter(
        (p) =>
          !!p.fullName &&
          p.fullName.toLowerCase().indexOf(filterValue.toLowerCase()) >= 0
      )
    : userStore.users;
});
onMounted(async () => {
  try {
    loading.value = true;
    await Promise.all([userStore.getUsers(), userStore.getWorkHourSums()]);
  } catch (error) {
    console.error(error);
    $q.notify({
      type: "negative",
      message: getApiErrorMessage(error, "Klarte ikke å hente brukere."),
    });
  } finally {
    loading.value = false;
  }
});

function getApprovedHours(userId: number): number {
  const sum = userStore.workHourSums.find((s) => s.userId === userId);
  return sum ? sum.approvedHours : 0;
}

// Startverdi emptyUser() i stedet for null slik at malen slipper null-sjekk;
// dialogen er lukket til editUser/newUser setter en ny verdi.
const selectedUser = ref<UserForm>(emptyUser());
function emptyUser(): UserForm {
  return {
    id: null,
    phoneNo: null,
    firstName: null,
    lastName: null,
    isAdmin: false,
    isHidden: false,
  };
}

const showingEditDialog = ref(false);

// Competency management
const loadingUserCompetencies = ref(false);
const adminSelectedCompetencyId = ref<number | null>(null);
const adminAddingCompetency = ref(false);
const approvingId = ref<number | null>(null);
const revokingId = ref<number | null>(null);

const editUserCompetencies = computed((): UserCompetencyResponse[] => {
  const userId = selectedUser.value?.id;
  if (!userId) return [];
  return competencyStore.userCompetencies[userId] || [];
});

const adminAvailableCompetencies = computed(() => {
  const existing = editUserCompetencies.value.map((uc) => uc.competencyId);
  return competencyStore.competencies.filter((c) => !existing.includes(c.id));
});

async function loadUserCompetencies(userId: number): Promise<void> {
  try {
    loadingUserCompetencies.value = true;
    await Promise.all([
      competencyStore.getUserCompetencies(userId),
      competencyStore.getCompetencies(),
    ]);
  } catch (error) {
    console.log(error);
  } finally {
    loadingUserCompetencies.value = false;
  }
}

function findCompetencyDef(competencyId: number) {
  return competencyStore.competencies.find((c) => c.id === competencyId);
}

async function approveCompetency(uc: UserCompetencyResponse): Promise<void> {
  const competencyDef = findCompetencyDef(uc.competencyId);
  if (competencyDef && competencyDef.hasExpiry) {
    $q.dialog({
      title: "Velg utløpsdato",
      message: `Kompetansen "${uc.competencyName}" krever en utløpsdato.`,
      prompt: {
        model: "",
        type: "date",
      },
      cancel: { label: "Avbryt", flat: true, noCaps: true },
      ok: {
        label: "Godkjenn",
        noCaps: true,
        color: "primary",
        unelevated: true,
      },
      persistent: true,
    }).onOk(async (expiryDate: string) => {
      await doApprove(uc, expiryDate || null);
    });
  } else {
    await doApprove(uc, null);
  }
}

// Kompetanse-seksjonen vises kun for eksisterende brukere, så
// selectedUser.value.id er satt i funksjonene under (derav `!`).
async function doApprove(
  uc: UserCompetencyResponse,
  expiryDate: string | null
): Promise<void> {
  try {
    approvingId.value = uc.id;
    await competencyStore.approveUserCompetency(uc.id, { expiryDate });
    await competencyStore.getUserCompetencies(selectedUser.value.id!);
    $q.notify({ message: "Kompetanse godkjent" });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Klarte ikke å godkjenne kompetanse",
      color: "negative",
    });
  } finally {
    approvingId.value = null;
  }
}

async function revokeCompetency(uc: UserCompetencyResponse): Promise<void> {
  try {
    revokingId.value = uc.id;
    await competencyStore.revokeUserCompetency(uc.id);
    await competencyStore.getUserCompetencies(selectedUser.value.id!);
    $q.notify({ message: "Kompetanse trukket tilbake" });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Klarte ikke å trekke tilbake kompetanse",
      color: "negative",
    });
  } finally {
    revokingId.value = null;
  }
}

async function adminAddCompetency(): Promise<void> {
  if (!adminSelectedCompetencyId.value) return;
  try {
    adminAddingCompetency.value = true;
    await competencyStore.addUserCompetency({
      userId: selectedUser.value.id!,
      competencyId: adminSelectedCompetencyId.value,
    });
    adminSelectedCompetencyId.value = null;
    await competencyStore.getUserCompetencies(selectedUser.value.id!);
    $q.notify({ message: "Kompetanse lagt til" });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Klarte ikke å legge til kompetanse",
      color: "negative",
    });
  } finally {
    adminAddingCompetency.value = false;
  }
}

function editUser(user: UserResponse): void {
  selectedUser.value = { ...user };
  adminSelectedCompetencyId.value = null;
  showingEditDialog.value = true;
  if (user.id) {
    loadUserCompetencies(user.id);
  }
}

function newUser(): void {
  selectedUser.value = emptyUser();
  showingEditDialog.value = true;
}

const saving = ref(false);
async function saveUser(): Promise<void> {
  try {
    saving.value = true;
    if (selectedUser.value.id) {
      await userStore.updateUser({
        ...selectedUser.value,
        id: selectedUser.value.id,
      });
    } else {
      await userStore.createUser(selectedUser.value);
    }
    $q.notify({ message: "Bruker lagret" });
    showingEditDialog.value = false;
  } catch (error) {
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å lagre bruker"),
    });
  } finally {
    saving.value = false;
  }
}

async function deleteUser(): Promise<void> {
  try {
    saving.value = true;
    // Slett-knappen vises kun når id er satt. Storen leser kun id.
    await userStore.deleteUser({ id: selectedUser.value.id! });
    $q.notify({ message: "Bruker slettet" });
    showingEditDialog.value = false;
  } catch {
    $q.notify({ message: "Klarte ikke å slette bruker" });
  } finally {
    saving.value = false;
  }
}
</script>
