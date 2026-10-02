<template>
  <q-page padding class="flex flex-center">
    <!-- content -->
    <div class="text-center" style="width: 100%; max-width: 800px">
      <q-form @submit="login">
        <q-card bordered flat>
          <q-card-section class="text-h6"
            ><img
              src="~assets/middagsasen-logo.svg"
              alt="Middagsåsen bemanning"
              style="max-width: 75vw; max-height: 200px"
          /></q-card-section>
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
              :loading="performingLogin"
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
    <q-btn
      class="absolute-top-right"
      flat
      round
      icon="ac_unit"
      to="/weather"
      size="lg"
    ></q-btn>
  </q-page>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { isAxiosError } from "axios";
import { api } from "boot/axios";
import { useAuthStore } from "src/stores/AuthStore";
import { useUserStore } from "src/stores/UserStore";
import { useRoute, useRouter } from "vue-router";
import { useQuasar } from "quasar";
import { isSafeRedirect } from "src/auth/unauthorizedHandler";
import type { AuthResponse } from "src/types";

const authStore = useAuthStore();
const userStore = useUserStore();
const router = useRouter();
const route = useRoute();
const $q = useQuasar();

const username = ref<string | null>(null);
const password = ref<string | null>(null);
const performingLogin = ref(false);

async function login(): Promise<void> {
  try {
    performingLogin.value = true;
    // Body er ikke typet som AuthRequest: feltene kan være null her, og
    // backend avviser det med 400 (som før).
    const response = await api.post<AuthResponse>(
      "/api/authentication/authenticate",
      {
        userName: username.value,
        password: password.value,
      }
    );

    // Backend svarer 200 kun ved AuthStatus.Success, og da er token satt.
    await authStore.setAccessToken(response.data.token!);
    await userStore.getUser();

    // getUser svelger feil (f.eks. 401/500/timeout mot /api/me). Uten bruker
    // må vi rydde bort tokenet og bli værende på innloggingssiden.
    if (!authStore.user) {
      authStore.removeUserSession();
      $q.notify({
        message: "Klarte ikke å logge deg på 😣",
      });
      return;
    }

    const redirect = route.query.redirect;
    await router.replace(isSafeRedirect(redirect) ? redirect : "/");
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Klarte ikke å logge deg på 😣",
    });
  } finally {
    performingLogin.value = false;
  }
}

const showingOtpDialog = ref(false);
const creatingOtp = ref(false);
async function createOtp(): Promise<void> {
  try {
    creatingOtp.value = true;
    await api.post("/api/authentication/otp", {
      userName: username.value,
    });
    showingOtpDialog.value = false;
    $q.notify({ message: "Engangskode er på vei på SMS 🙌" });
  } catch (error) {
    // Backend svarer 429 ved OtpStatus.TooManyRequests og 400 ved
    // OtpStatus.InvalidPhoneNumber.
    if (isAxiosError(error) && error.response?.status === 429)
      $q.notify({
        message:
          "Du har nettopp prøvd å hente engangskode, vent 5 min før du prøver igjen ✋",
      });
    else if (isAxiosError(error) && error.response?.status === 400)
      $q.notify({
        message: "Sjekk at telefonnummeret er riktig ✋",
      });
    else
      $q.notify({
        message: "Klarte ikke å lage engangskode 😳",
      });
  } finally {
    creatingOtp.value = false;
  }
}
</script>
