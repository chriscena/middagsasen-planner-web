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
    <q-dialog v-model="showingUserDialog" persistent>
      <q-card class="full-width">
        <q-form @submit="saveUser">
          <q-card-section class="text-h6">Brukerinfo</q-card-section>
          <q-card-section class="row q-col-gutter-sm">
            <q-input
              class="col-12"
              :model-value="user.phoneNo"
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

<script setup>
import { computed, reactive, ref, watch } from "vue";
import { useAuthStore } from "src/stores/AuthStore";
import { useUserStore } from "src/stores/UserStore";
import { useCompetencyStore } from "src/stores/CompetencyStore";
import { useVuelidate } from "@vuelidate/core";
import { required } from "@vuelidate/validators";
import { useRouter } from "vue-router";
import { useQuasar, date as dateUtil } from "quasar";

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
const selectedCompetencyId = ref(null);

const myCompetencies = computed(() => {
  const userId = user.value?.id;
  if (!userId) return [];
  return competencyStore.userCompetencies[userId] || [];
});

const availableCompetencies = computed(() => {
  const existing = myCompetencies.value.map((uc) => uc.competencyId);
  return competencyStore.competencies.filter((c) => !existing.includes(c.id));
});

function formatDate(dateStr) {
  if (!dateStr) return "";
  return dateUtil.formatDate(new Date(dateStr), "DD.MM.YYYY");
}

async function loadMyCompetencies() {
  const userId = user.value?.id;
  if (!userId) return;
  try {
    loadingCompetencies.value = true;
    await Promise.all([
      competencyStore.getUserCompetencies(userId),
      competencyStore.getCompetencies(),
    ]);
  } catch (error) {
    console.log(error);
  } finally {
    loadingCompetencies.value = false;
  }
}

async function addMyCompetency() {
  if (!selectedCompetencyId.value) return;
  try {
    addingCompetency.value = true;
    await competencyStore.addUserCompetency({
      userId: user.value.id,
      competencyId: selectedCompetencyId.value,
    });
    selectedCompetencyId.value = null;
    await competencyStore.getUserCompetencies(user.value.id);
    $q.notify({ message: "Kompetanse registrert" });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å registrere kompetanse", color: "negative" });
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

const state = reactive({
  firstName: null,
  lastName: null,
  password: null,
});

const saving = ref(false);

const rules = {
  firstName: { required },
  lastName: { required },
  password: {},
};

const v$ = useVuelidate(rules, state);

function editUser() {
  state.firstName = user.value?.firstName;
  state.lastName = user.value?.lastName;
  state.password = null;
  editingUser.value = true;
}

async function saveUser() {
  try {
    saving.value = true;
    const model = {
      firstName: state.firstName,
      lastName: state.lastName,
      password: state.password,
    };
    await userStore.saveUser(model);
    $q.notify({ message: "Endringer er lagret" });
    editingUser.value = false;
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å lagre endringer" });
  } finally {
    saving.value = false;
  }
}

async function updateHidden(isHidden) {
  try {
    saving.value = true;
    const model = {
      isHidden: isHidden,
    };
    await userStore.saveUser(model);
    $q.notify({
      message: model.isHidden
        ? "Du er nå skjult fra telefonlisten 👻"
        : "Du vises nå i telefonlisten 🙋‍♂️",
    });
    editingUser.value = false;
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å lagre endringen" });
  } finally {
    saving.value = false;
  }
}

async function logout() {
  await userStore.logout();
  $q.notify({ message: "Du er logget ut" });
  router.push("/login");
}

const leftDrawerOpen = ref(false);

function toggleLeftDrawer() {
  leftDrawerOpen.value = !leftDrawerOpen.value;
}

function toggleRightDrawer() {
  rightDrawerOpen.value = !rightDrawerOpen.value;
}
</script>
