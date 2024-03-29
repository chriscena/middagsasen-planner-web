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
        <q-toolbar-title>Telefonliste</q-toolbar-title>
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
      <q-item separator v-for="user in phoneList" :key="user.id">
        <q-item-section>
          <q-item-label lines="1">{{ user.fullName }}</q-item-label>
          <q-item-label caption lines="1">{{ user.phoneNo }}</q-item-label>
        </q-item-section>

        <q-item-section side>
          <div class="row q-gutter-md">
            <q-btn
              type="a"
              :href="'sms:' + user.phoneNo"
              flat
              round
              text-color="primary"
              icon="sms"
              :title="`Send SMS til ${user.fullName}`"
            ></q-btn>
            <q-btn
              type="a"
              :href="'tel:' + user.phoneNo"
              flat
              round
              text-color="primary"
              icon="call"
              :title="`Ring til ${user.fullName}`"
            ></q-btn>
          </div>
        </q-item-section>
      </q-item>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useQuasar } from "quasar";
import { useUserStore } from "stores/UserStore";
import { useRouter } from "vue-router";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const router = useRouter();
const userStore = useUserStore();

const filter = ref(null);

const phoneList = computed(() =>
  !!filter.value
    ? userStore.phoneList.filter(
        (p) =>
          !!p.fullName &&
          p.fullName.toLowerCase().indexOf(filter.value.toLowerCase()) >= 0
      )
    : userStore.phoneList
);
onMounted(async () => {
  try {
    loading.value = true;
    await userStore.getPhoneList();
  } catch (error) {
  } finally {
    loading.value = false;
  }
});
</script>
