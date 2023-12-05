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
        ></q-btn>
        <q-toolbar-title>Brukere</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <q-input
      outlined
      clearable
      v-model="filter"
      debounce="500"
      placeholder="Søk"
    >
      <template v-slot:prepend>
        <q-icon name="search"></q-icon>
      </template>
    </q-input>

    <q-list separator>
      <q-item separator v-for="user in users" :key="user.id">
        <q-item-section>
          <q-item-label lines="1"
            >{{ user.fullName }}
            <q-icon v-if="user.isAdmin" color="primary" name="shield"></q-icon
          ></q-item-label>
          <q-item-label caption lines="1">{{ user.phoneNo }}</q-item-label>
        </q-item-section>

        <q-item-section side>
          <q-btn flat round icon="edit" @click="editUser(user)"></q-btn>
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
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEditDialog" persistent>
      <q-card class="full-width">
        <q-card-section class="row"
          ><span>{{ selectedUser.id ? "Endre" : "Legg til" }}</span
          ><q-space></q-space
          ><q-btn
            v-if="selectedUser.id"
            flat
            round
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
          ></q-checkbox
          ><q-checkbox
            label="Skjul fra telefonlista"
            v-model="selectedUser.isHidden"
          ></q-checkbox>
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

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useQuasar } from "quasar";
import { useUserStore } from "stores/UserStore";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const router = useRouter();
const userStore = useUserStore();

const filter = ref(null);

const users = computed(() =>
  !!filter.value
    ? userStore.users.filter(
        (p) =>
          !!p.fullName &&
          p.fullName.toLowerCase().indexOf(filter.value.toLowerCase()) >= 0
      )
    : userStore.users
);
onMounted(async () => {
  try {
    loading.value = true;
    await userStore.getUsers();
  } catch (error) {
  } finally {
    loading.value = false;
  }
});

const selectedUser = ref(null);
function emptyUser() {
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
function editUser(user) {
  selectedUser.value = { ...user };
  showingEditDialog.value = true;
}

function newUser() {
  selectedUser.value = emptyUser();
  showingEditDialog.value = true;
}

const saving = ref(false);
async function saveUser() {
  try {
    saving.value = true;
    if (selectedUser.value.id) {
      await userStore.updateUser(selectedUser.value);
    } else {
      await userStore.createUser(selectedUser.value);
    }
    $q.notify({ message: "Bruker lagret" });
    showingEditDialog.value = false;
  } catch (error) {
    $q.notify({ message: "Klarte ikke å lagre bruker" });
  } finally {
    saving.value = true;
  }
}

async function deleteUser() {
  try {
    saving.value = true;
    await userStore.deleteUser(selectedUser.value);
    $q.notify({ message: "Bruker slettet" });
    showingEditDialog.value = false;
  } catch (error) {
    $q.notify({ message: "Klarte ikke å slette bruker" });
  } finally {
    saving.value = true;
  }
}
</script>
