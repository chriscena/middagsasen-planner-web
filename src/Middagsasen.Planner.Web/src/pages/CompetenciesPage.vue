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
        <q-toolbar-title>Kompetanser</q-toolbar-title>
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
    <q-list role="list" separator>
      <q-item
        v-for="competency in competencyStore.competencies"
        :key="competency.id"
        clickable
        @click="editCompetency(competency)"
        v-ripple
        title="Endre kompetanse"
      >
        <q-item-section>
          <q-item-label>{{ competency.name }}</q-item-label>
          <q-item-label caption>{{ competency.description }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-badge color="primary">
            {{ competency.resourceTypes?.length || 0 }}
            {{ (competency.resourceTypes?.length || 0) === 1 ? "vakttype" : "vakttyper" }}
          </q-badge>
        </q-item-section>
      </q-item> </q-list
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
          @click="newCompetency"
          title="Legg til ny kompetanse"
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEdit" persistent>
      <q-card class="full-width full-height">
        <q-card-section class="row"
          ><span class="text-h6">{{
            !selected.id ? "Ny kompetanse" : "Endre kompetanse"
          }}</span
          ><q-space></q-space>
          <q-btn
            v-if="selected.id"
            color="negative"
            round
            flat
            dense
            icon="delete"
            title="Slett kompetanse"
            @click="deleteCompetency"
          ></q-btn
        ></q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input
            autofocus
            outlined
            label="Navn"
            v-model="selected.name"
          ></q-input>
          <q-input
            outlined
            label="Beskrivelse"
            v-model="selected.description"
            type="textarea"
            autogrow
          ></q-input>
          <q-checkbox
            v-model="selected.hasExpiry"
            label="Har utl&#248;psdato"
          ></q-checkbox>

          <q-card bordered flat>
            <q-card-section class="q-py-sm text-subtitle2"
              >Godkjennere</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item
                v-for="approver in selected.approvers"
                :key="approver.id"
              >
                <q-item-section>{{ approver.fullName }}</q-item-section>
                <q-item-section side>
                  <q-btn
                    flat
                    round
                    icon="delete"
                    title="Fjern godkjenner"
                    @click="removeApprover(approver)"
                  ></q-btn>
                </q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-actions>
              <q-select
                class="col"
                label="Bruker"
                placeholder="Velg bruker"
                outlined
                dense
                :options="availableApprovers"
                option-label="fullName"
                option-value="id"
                v-model="selectedApprover"
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
              <q-btn
                icon="add"
                flat
                round
                color="primary"
                :disable="!selectedApprover"
                @click="addApprover"
                title="Legg til godkjenner"
              ></q-btn>
            </q-card-actions>
          </q-card>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Avbryt" no-caps v-close-popup></q-btn>
          <q-btn
            unelevated
            label="Lagre"
            color="primary"
            @click="saveCompetency"
            no-caps
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { onMounted, ref, computed } from "vue";
import { useRouter } from "vue-router";
import { useQuasar } from "quasar";
import { useCompetencyStore } from "stores/CompetencyStore";
import { useUserStore } from "stores/UserStore";

const emit = defineEmits(["toggle-right"]);
const $router = useRouter();
const competencyStore = useCompetencyStore();
const userStore = useUserStore();
const $q = useQuasar();

const showingEdit = ref(false);
const loading = ref(false);
const selected = ref(emptyCompetency());
const selectedApprover = ref(null);

onMounted(async () => {
  try {
    loading.value = true;
    await Promise.all([
      competencyStore.getCompetencies(),
      userStore.getUsers(),
    ]);
  } catch (error) {
    console.log(error);
  } finally {
    loading.value = false;
  }
});

const availableApprovers = computed(() => {
  const approverUserIds = selected.value.approvers.map((a) => a.userId);
  return userStore.users.filter((u) => !approverUserIds.includes(u.id));
});

function emptyCompetency() {
  return {
    id: null,
    name: null,
    description: null,
    hasExpiry: false,
    approvers: [],
  };
}

function newCompetency() {
  selected.value = emptyCompetency();
  showingEdit.value = true;
}

async function editCompetency(competency) {
  try {
    const full = await competencyStore.getCompetencyById(competency.id);
    selected.value = {
      id: full.id,
      name: full.name,
      description: full.description,
      hasExpiry: full.hasExpiry,
      approvers: [...(full.approvers || [])],
    };
    showingEdit.value = true;
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å hente kompetanse." });
  }
}

async function saveCompetency() {
  const request = {
    name: selected.value.name,
    description: selected.value.description,
    hasExpiry: selected.value.hasExpiry,
  };
  try {
    if (selected.value.id) {
      await competencyStore.updateCompetency(selected.value.id, request);
    } else {
      await competencyStore.createCompetency(request);
    }
    showingEdit.value = false;
    $q.notify({ message: "Kompetansen er lagret." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å lagre kompetansen." });
  }
}

async function deleteCompetency() {
  try {
    await competencyStore.deleteCompetency(selected.value.id);
    showingEdit.value = false;
    $q.notify({ message: "Kompetansen er slettet." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å slette kompetansen." });
  }
}

async function addApprover() {
  if (!selected.value.id) {
    selected.value.approvers.push({
      id: 0,
      userId: selectedApprover.value.id,
      fullName: selectedApprover.value.fullName,
    });
    selectedApprover.value = null;
    return;
  }
  try {
    const approver = await competencyStore.addApprover(
      selected.value.id,
      selectedApprover.value.id
    );
    selected.value.approvers.push(approver);
    selectedApprover.value = null;
    $q.notify({ message: "Godkjenner er lagt til." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å legge til godkjenner." });
  }
}

async function removeApprover(approver) {
  if (!selected.value.id || !approver.id) {
    selected.value.approvers = selected.value.approvers.filter(
      (a) => a !== approver
    );
    return;
  }
  try {
    await competencyStore.removeApprover(approver.id);
    selected.value.approvers = selected.value.approvers.filter(
      (a) => a.id !== approver.id
    );
    $q.notify({ message: "Godkjenner er fjernet." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å fjerne godkjenner." });
  }
}
</script>
