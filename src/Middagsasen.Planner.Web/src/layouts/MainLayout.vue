<template>
  <q-layout view="hHh lpR fFf">
    <q-drawer
      :bordered="!$q.platform.is.mobile"
      :elevated="$q.platform.is.mobile"
      v-model="leftDrawerOpen"
      side="left"
    >
      <q-toolbar
        ><q-space></q-space
        ><q-btn
          text-color="blue-grey-8 "
          flat
          round
          dense
          icon="close"
          title="Lukk"
          @click="leftDrawerOpen = false"
        ></q-btn
      ></q-toolbar>
      <q-separator></q-separator>
      <q-list separator>
        <q-item clickable to="/" v-ripple>
          <q-item-section avatar>
            <q-icon name="calendar_today"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Kalender</q-item-label>
          </q-item-section>
        </q-item>
        <q-item clickable to="/phonelist" v-ripple>
          <q-item-section avatar>
            <q-icon name="contacts"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Telefonliste</q-item-label>
          </q-item-section>
        </q-item>
        <q-item clickable to="/weather" v-ripple>
          <q-item-section avatar>
            <q-icon name="ac_unit"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Værdata</q-item-label>
          </q-item-section>
        </q-item>

        <q-item v-if="isAdmin" to="/templates" v-ripple>
          <q-item-section avatar>
            <q-icon name="edit_calendar"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Maler</q-item-label>
          </q-item-section>
        </q-item>

        <q-item v-if="isAdmin" to="/users" v-ripple>
          <q-item-section avatar>
            <q-icon name="group"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Brukere</q-item-label>
          </q-item-section>
        </q-item>

        <q-item v-if="isAdmin" clickable to="/competencies" v-ripple>
          <q-item-section avatar>
            <q-icon name="workspace_premium"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Kompetanse</q-item-label>
          </q-item-section>
        </q-item>

        <q-item v-if="isAdmin" clickable to="/resourcetypes" v-ripple>
          <q-item-section avatar>
            <q-icon name="settings"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Vakttyper</q-item-label>
          </q-item-section>
        </q-item>

        <q-item v-if="isAdmin" clickable to="/approveHours" v-ripple>
          <q-item-section avatar>
            <q-icon name="alarm_on"></q-icon>
          </q-item-section>
          <q-item-section>
            <q-item-label>Timelister</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
      <q-separator></q-separator>
      <div class="q-px-md q-py-sm text-caption text-grey">{{ appVersion }}</div>
    </q-drawer>

    <q-drawer elevated v-model="rightDrawerOpen" side="right" overlay>
      <q-toolbar
        ><q-btn
          text-color="blue-grey-8 "
          flat
          dense
          round
          icon="close"
          title="Lukk"
          @click="rightDrawerOpen = false"
        ></q-btn
      ></q-toolbar>
      <q-separator></q-separator>
      <q-list role="list" separator>
        <q-item clickable @click="editUser" v-ripple>
          <q-item-section avatar
            ><q-icon name="person"></q-icon
          ></q-item-section>
          <q-item-section>
            <q-item-label>{{ user?.fullName }}</q-item-label>
            <q-item-label caption>{{ user?.phoneNo }}</q-item-label>
          </q-item-section>
        </q-item>
        <q-item>
          <q-item-section>
            <q-item-label>Skjul fra telefonliste</q-item-label>
          </q-item-section>
          <q-item-section side>
            <q-toggle
              :model-value="user?.isHidden"
              @update:model-value="updateHidden"
            ></q-toggle>
          </q-item-section>
        </q-item>
        <q-item>
          <q-item-section>
            <q-item-label>SMS-påminnelse dagen før vakt</q-item-label>
            <q-item-label caption
              >Én SMS kvelden før med vaktene dine for neste dag</q-item-label
            >
          </q-item-section>
          <q-item-section side>
            <q-toggle
              :model-value="user?.shiftReminders"
              @update:model-value="updateShiftReminders"
            ></q-toggle>
          </q-item-section>
        </q-item>
        <q-item clickable to="/shifts">
          <q-item-section avatar><q-icon name="list"></q-icon></q-item-section>
          <q-item-section>
            <q-item-label>Mine vakter</q-item-label>
          </q-item-section>
        </q-item>
        <q-item clickable to="/hours">
          <q-item-section avatar
            ><q-icon name="history"></q-icon
          ></q-item-section>
          <q-item-section>
            <q-item-label>Mine timer</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
      <q-separator></q-separator>
      <q-list>
        <q-item-label header>Mine kompetanser</q-item-label>
        <q-item v-if="loadingCompetencies">
          <q-item-section>
            <q-spinner size="1.5em" color="primary"></q-spinner>
          </q-item-section>
        </q-item>
        <q-item v-for="uc in myCompetencies" :key="uc.id">
          <q-item-section>
            <q-item-label>{{ uc.competencyName }}</q-item-label>
            <q-item-label caption v-if="uc.expiryDate">
              Utløper: {{ formatDate(uc.expiryDate) }}
            </q-item-label>
          </q-item-section>
          <q-item-section side>
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
          </q-item-section>
        </q-item>
        <q-item v-if="!loadingCompetencies && myCompetencies.length === 0">
          <q-item-section>
            <q-item-label caption>Ingen kompetanser registrert</q-item-label>
          </q-item-section>
        </q-item>
        <q-item>
          <q-item-section>
            <q-select
              v-model="selectedCompetencyId"
              :options="availableCompetencies"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              outlined
              dense
              label="Legg til kompetanse"
            ></q-select>
          </q-item-section>
          <q-item-section side>
            <q-btn
              flat
              round
              dense
              icon="add"
              color="primary"
              :disable="!selectedCompetencyId"
              :loading="addingCompetency"
              @click="addMyCompetency"
            ></q-btn>
          </q-item-section>
        </q-item>
      </q-list>
      <q-separator></q-separator>
      <q-list separator>
        <q-item clickable @click="logout">
          <q-item-section avatar
            ><q-icon name="logout"></q-icon
          ></q-item-section>
          <q-item-section>
            <q-item-label>Logg ut</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </q-drawer>

    <q-page-container>
      <router-view
        @toggle-left="toggleLeftDrawer"
        @toggle-right="toggleRightDrawer"
      />
    </q-page-container>
    <!-- Dialogen vises kun når user er satt (showingUserDialog), derav `!`. -->
    <q-dialog v-model="showingUserDialog" persistent>
      <q-card class="full-width">
        <q-form @submit="saveUser">
          <q-card-section class="text-h6">Brukerinfo</q-card-section>
          <q-card-section class="row q-col-gutter-sm">
            <q-input
              class="col-12"
              :model-value="user!.phoneNo"
              outlined
              label="Mobiltelefon"
              readonly
              autocomplete="username"
            ></q-input>
            <q-input
              class="col-12"
              @blur="v$.firstName.$touch"
              v-model="v$.firstName.$model"
              :error="v$.firstName.$error"
              autofocus
              outlined
              label="Fornavn"
              autocomplete="given-name"
              hide-bottom-space
            ></q-input>
            <q-input
              class="col-12"
              @blur="v$.lastName.$touch"
              v-model="v$.lastName.$model"
              :error="v$.lastName.$error"
              outlined
              label="Etternavn"
              autocomplete="family-name"
              hide-bottom-space
            ></q-input>
            <q-input
              class="col-12"
              outlined
              label="Passord (valgfritt)"
              type="password"
              autocomplete="new-password"
              v-model="state.password"
              hint="Skriv inn passord om du vil slippe engangspassord. La feltet være tomt om du ikke vil legge til/endre passord."
            ></q-input>
          </q-card-section>
          <q-card-actions align="right"
            ><q-btn
              v-if="editingUser"
              flat
              no-caps
              label="Avbryt"
              :disable="saving"
              @click.stop="editingUser = false"
            ></q-btn
            ><q-btn
              no-caps
              color="primary"
              unelevated
              type="submit"
              label="Lagre"
              :disable="!!v$.$silentErrors.length"
              :loading="saving"
            ></q-btn></q-card-actions></q-form
      ></q-card>
    </q-dialog>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { useAuthStore } from "@/stores/AuthStore";
import { useUserStore } from "@/stores/UserStore";
import { useCompetencyStore } from "@/stores/CompetencyStore";
import { useVuelidate } from "@vuelidate/core";
import type { ValidationArgs } from "@vuelidate/core";
import { required } from "@vuelidate/validators";
import { useRouter } from "vue-router";
import { useQuasar } from "quasar";
import { formatVersion } from "@/shared/appVersion";
import { notifyApiError } from "@/shared/notifyApiError";
import { formatDate } from "@/shared/time";
import type { UpdateMeRequest, UserCompetencyResponse } from "@/types";

// Skjemaet i brukerinfo-dialogen.
interface UserForm {
  firstName: string | null;
  lastName: string | null;
  password: string | null;
}

// Byggversjon vises nederst i menyen.
const appVersion = formatVersion(__APP_VERSION__);

const authStore = useAuthStore();
const userStore = useUserStore();
const competencyStore = useCompetencyStore();
const router = useRouter();
const $q = useQuasar();

const user = computed(() => authStore.user);
const isAdmin = computed(() => user.value?.isAdmin ?? false);

// Competency management
const loadingCompetencies = ref(false);
const addingCompetency = ref(false);
const selectedCompetencyId = ref<number | null>(null);

const myCompetencies = computed((): UserCompetencyResponse[] => {
  const userId = user.value?.id;
  if (!userId) return [];
  return competencyStore.userCompetencies[userId] || [];
});

const availableCompetencies = computed(() => {
  const existing = myCompetencies.value.map((uc) => uc.competencyId);
  return competencyStore.competencies.filter((c) => !existing.includes(c.id));
});

async function loadMyCompetencies(): Promise<void> {
  const userId = user.value?.id;
  if (!userId) return;
  try {
    loadingCompetencies.value = true;
    await Promise.all([
      competencyStore.getUserCompetencies(userId),
      competencyStore.getCompetencies(),
    ]);
  } catch (error) {
    notifyApiError(error, "Klarte ikke å hente kompetanser");
  } finally {
    loadingCompetencies.value = false;
  }
}

// Høyremenyen (med knappen) brukes kun av innloggede brukere, så user er satt.
async function addMyCompetency(): Promise<void> {
  if (!selectedCompetencyId.value) return;
  try {
    addingCompetency.value = true;
    await competencyStore.addUserCompetency({
      userId: user.value!.id,
      competencyId: selectedCompetencyId.value,
    });
    selectedCompetencyId.value = null;
    await competencyStore.getUserCompetencies(user.value!.id);
    $q.notify({ message: "Kompetanse registrert" });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å registrere kompetanse");
  } finally {
    addingCompetency.value = false;
  }
}

// Load competencies when right drawer opens and user is logged in
const rightDrawerOpen = ref(false);
watch(rightDrawerOpen, (val) => {
  if (val && user.value?.id) {
    loadMyCompetencies();
  }
});

const editingUser = ref(false);

const showingUserDialog = computed(
  () =>
    !!user.value &&
    !!user.value.id &&
    (editingUser.value || !user.value.firstName || !user.value.lastName)
);

const state = reactive<UserForm>({
  firstName: null,
  lastName: null,
  password: null,
});

const saving = ref(false);

const rules = {
  firstName: { required },
  lastName: { required },
  password: {},
} satisfies ValidationArgs<UserForm>;

const v$ = useVuelidate(rules, state);

function editUser(): void {
  // `?? null`: feltene er valgfrie i UserResponse; backend sender null fremfor
  // å utelate dem, så verdien er den samme som i JS-versjonen.
  state.firstName = user.value?.firstName ?? null;
  state.lastName = user.value?.lastName ?? null;
  state.password = null;
  editingUser.value = true;
}

async function saveUser(): Promise<void> {
  try {
    saving.value = true;
    const model: UpdateMeRequest = {
      firstName: state.firstName,
      lastName: state.lastName,
      password: state.password,
    };
    await userStore.saveUser(model);
    $q.notify({ message: "Endringer er lagret" });
    editingUser.value = false;
  } catch (error) {
    notifyApiError(error, "Klarte ikke å lagre endringer");
  } finally {
    saving.value = false;
  }
}

// Lagrer én enkelt innstilling (toggle) på innlogget bruker og viser bekreftelse.
async function saveMySetting(
  model: UpdateMeRequest,
  message: string
): Promise<void> {
  try {
    saving.value = true;
    await userStore.saveUser(model);
    $q.notify({ message });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å lagre endringen");
  } finally {
    saving.value = false;
  }
}

function updateHidden(isHidden: boolean): Promise<void> {
  return saveMySetting(
    { isHidden },
    isHidden
      ? "Du er nå skjult fra telefonlisten 👻"
      : "Du vises nå i telefonlisten 🙋‍♂️"
  );
}

function updateShiftReminders(shiftReminders: boolean): Promise<void> {
  return saveMySetting(
    { shiftReminders },
    shiftReminders
      ? "Du får nå SMS-påminnelse dagen før vakt 🔔"
      : "SMS-påminnelse er slått av"
  );
}

async function logout(): Promise<void> {
  await userStore.logout();
  $q.notify({ message: "Du er logget ut" });
  router.push("/login");
}

const leftDrawerOpen = ref(false);

function toggleLeftDrawer(): void {
  leftDrawerOpen.value = !leftDrawerOpen.value;
}

function toggleRightDrawer(): void {
  rightDrawerOpen.value = !rightDrawerOpen.value;
}
</script>
