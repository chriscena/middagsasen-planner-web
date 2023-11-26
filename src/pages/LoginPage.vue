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
      <q-form @submit="createOtp">
        <q-card>
          <q-card-section class="text-h6"
            >Få tilsendt engangskode</q-card-section
          >
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
              type="submit"
              unelevated
              color="primary"
            ></q-btn>
          </q-card-actions> </q-card
      ></q-form>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { ref } from "vue";

const username = ref(null);
const password = ref(null);

const showingOtpDialog = ref(false);

function login() {
  console.log("login");
}
function createOtp() {
  console.log("otp");
  showingOtpDialog.value = false;
}
</script>
