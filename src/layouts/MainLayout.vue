<template>
  <q-layout view="hHh lpR fFf">
    <q-drawer v-model="leftDrawerOpen" side="left">
      <!-- drawer content -->
    </q-drawer>

    <q-drawer v-model="rightDrawerOpen" side="right" overlay>
      <!-- drawer content -->
    </q-drawer>

    <q-page-container>
      <router-view
        @toggle-left="toggleLeftDrawer"
        @toggle-right="toggleRightDrawer"
      />
    </q-page-container>
    <q-dialog v-model="showingUserDialog">
      <q-card class="full-width">
        <q-form @submit="saveUser">
          <q-card-section class="">Ny bruker</q-card-section>
          <q-card-section class="row q-col-gutter-sm">
            <q-input
              class="col-12"
              :model-value="user.userName"
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
              label="Passord"
              type="password"
              autocomplete="new-password"
              v-model="state.password"
              hint="Skriv inn passord om du vil slippe engangspassord."
            ></q-input>
          </q-card-section>
          <q-card-actions align="right"
            ><q-btn
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
import { computed, reactive, ref } from "vue";
import { useAuthStore } from "src/stores/AuthStore";
import { useUserStore } from "src/stores/UserStore";
import { useVuelidate } from "@vuelidate/core";
import { required } from "@vuelidate/validators";

const authStore = useAuthStore();
const userStore = useUserStore();

const user = computed(() => authStore.user);

const showingUserDialog = computed(
  () =>
    !!user.value &&
    !!user.value.id &&
    (!user.value.firstName || !user.value.lastName)
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

async function saveUser() {
  try {
    saving.value = true;
    const model = {
      firstName: state.firstName,
      lastName: state.lastName,
      password: state.password,
    };
    await userStore.saveUser(model);
  } catch (error) {
    console.log(error);
  } finally {
    saving.value = false;
  }
}

const leftDrawerOpen = ref(false);
const rightDrawerOpen = ref(false);

function toggleLeftDrawer() {
  leftDrawerOpen.value = !leftDrawerOpen.value;
}

function toggleRightDrawer() {
  rightDrawerOpen.value = !rightDrawerOpen.value;
}
</script>
