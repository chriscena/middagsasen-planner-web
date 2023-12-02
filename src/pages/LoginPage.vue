<template>
  <q-page padding class="flex flex-center">
    <!-- content -->
    <div class="text-center" style="width: 100%; max-width: 800px">
      <q-form @submit="login">
        <q-card bordered flat>
          <q-card-section class="text-h6">Middagsåsen bemanning</q-card-section>
          <q-card-section class="q-col-gutter-sm row">
            <q-input
              class="col-12"
              outlined
              autofocus
              autocomplete="username"
              label="Mobiltelefon"
              v-model="username"
            ></q-input>
            <q-input
              class="col-12"
              outlined
              autocomplete="password"
              type="password"
              label="Passord/engangskode"
              v-model="password"
            ></q-input>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn
              label="Logg inn"
              no-caps
              type="submit"
              unelevated
              color="primary"
            ></q-btn>
          </q-card-actions> </q-card
      ></q-form>

      <q-btn
        color="primary"
        class="q-mt-md"
        no-caps
        flat
        @click="showingOtpDialog = true"
        >Opprette konto eller glemt passord?</q-btn
      >
    </div>
    <q-dialog v-model="showingOtpDialog" persistent>
      <q-card>
        <q-card-section class="text-h6">Få tilsendt engangskode</q-card-section>
        <q-card-section class="">
          <q-input
            class="col-12"
            outlined
            autofocus
            autocomplete="username"
            label="Mobiltelefon"
            v-model="username"
          ></q-input
        ></q-card-section>
        <q-card-actions align="right">
          <q-btn no-caps label="Avbryt" flat v-close-popup></q-btn>
          <q-btn
            label="Hent kode"
            no-caps
            @click="createOtp"
            unelevated
            color="primary"
            :loading="creatingOtp"
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { ref } from "vue";
import { api } from "boot/axios";
import { useAuthStore } from "src/stores/AuthStore";
import { useRouter } from "vue-router";
import { useQuasar } from "quasar";

const authStore = useAuthStore();
const router = useRouter();

const username = ref(null);
const password = ref(null);
const $q = useQuasar();

const performingLogin = ref(false);
async function login() {
  try {
    performingLogin.value = true;
    const response = await api.post("/api/authentication/authenticate", {
      userName: username.value,
      password: password.value,
    });

    authStore.setAccessToken(response.data.token);
    router.push("/");
  } catch (error) {
    console.log(error);
  } finally {
    performingLogin.value = false;
  }
}

const showingOtpDialog = ref(false);
const creatingOtp = ref(false);
async function createOtp() {
  try {
    creatingOtp.value = true;
    await api.post("/api/authentication/otp", {
      userName: username.value,
    });
    showingOtpDialog.value = false;
    $q.notify({ type: "positive", message: "Engangskode er på vei." });
  } catch (error) {
    console.log(error);
  } finally {
    creatingOtp.value = false;
  }
}
</script>
