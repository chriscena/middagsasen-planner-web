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
        <q-toolbar-title>Vakttype</q-toolbar-title>
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
        v-for="resourceType in eventStore.resourceTypes"
        :key="resourceType.id"
        clickable
        @click="editResourceType(resourceType)"
        v-ripple
        title="Endre vakttype"
      >
        <q-item-section>
          <q-item-label>{{ resourceType.name }} </q-item-label
          ><q-item-label caption
            >{{ resourceType.defaultStaff }}
            {{ resourceType.defaultStaff === 1 ? "vakt" : "vakter" }}
          </q-item-label>
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
          @click="newResourceType"
          title="Legg til ny vakttype"
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEdit">
      <q-card class="full-width full-height">
        <q-card-section class="row"
          ><span class="text-h6">{{
            !selectedResource!.id ? "Ny vakttype" : "Endre vakttype"
          }}</span
          ><q-space></q-space>
          <q-btn
            v-if="selectedResource!.id"
            color="negative"
            round
            flat
            dense
            icon="delete"
            title="Slett vakttype"
            @click="confirmDeleteResourceType"
          ></q-btn
        ></q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input
            autofocus
            outlined
            label="Navn"
            v-model="selectedResource!.name"
          ></q-input>
          <q-input
            outlined
            label="Antall"
            v-model="selectedResource!.defaultStaff"
            suffix="stk"
            @focus="(event) => (event.target as HTMLInputElement).select()"
          ></q-input>

          <q-input
            outlined
            label="Varsel"
            v-model="selectedResource!.notificationMessage"
            type="textarea"
            autogrow
            clearable
          ></q-input>
          <q-card bordered flat>
            <q-card-section class="q-py-sm text-subtitle2"
              >Opplæringsansvarlig</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item
                v-for="trainer in visibleTrainers"
                :key="trainer.clientKey"
              >
                <q-item-section>{{ trainer.fullName }}</q-item-section>
                <q-item-section side
                  ><q-btn
                    flat
                    round
                    icon="delete"
                    title="Slette ansvarlig"
                    @click="deleteTrainer(trainer)"
                  ></q-btn
                ></q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-actions align="right">
              <q-btn
                icon="add"
                label="Legg til ansvarlig"
                no-caps
                flat
                color="primary"
                @click="showAddTrainer"
              ></q-btn>
            </q-card-actions>
          </q-card>
          <q-card bordered flat>
            <q-card-section class="q-py-sm text-subtitle2"
              >Kompetansekrav</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item
                v-for="(req, index) in competencyRequirements"
                :key="req.competencyId"
              >
                <q-item-section>
                  {{ req.competencyName }}
                </q-item-section>
                <q-item-section side style="min-width: 80px">
                  <q-input
                    outlined
                    dense
                    type="number"
                    v-model.number="req.minimumRequired"
                    :min="1"
                    label="Min"
                    @focus="
                      (event) => (event.target as HTMLInputElement).select()
                    "
                  ></q-input>
                </q-item-section>
                <q-item-section side>
                  <q-btn
                    flat
                    round
                    icon="delete"
                    title="Fjern kompetansekrav"
                    @click="removeCompetencyRequirement(index)"
                  ></q-btn>
                </q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-actions>
              <q-select
                class="col"
                label="Kompetanse"
                placeholder="Velg kompetanse"
                outlined
                dense
                :options="availableCompetencies"
                option-label="name"
                option-value="id"
                v-model="selectedCompetency"
              ></q-select>
              <q-btn
                icon="add"
                flat
                round
                color="primary"
                :disable="!selectedCompetency"
                @click="addCompetencyRequirement"
                title="Legg til kompetansekrav"
              ></q-btn>
            </q-card-actions>
          </q-card>
          <q-card bordered flat>
            <q-card-section class="q-py-sm text-subtitle2">Filer</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item v-for="file in selectedResource!.files" :key="file.id">
                <q-item-section
                  ><q-item-label>{{ file.description }}</q-item-label>
                  <q-item-label caption>{{
                    file.fileName
                  }}</q-item-label></q-item-section
                >
                <q-item-section side
                  ><q-btn
                    flat
                    round
                    icon="download"
                    title="Last ned fil"
                    @click="downloadResourceTypeFileOrNotify(file, $q.notify)"
                  ></q-btn> </q-item-section
                ><q-item-section side>
                  <q-btn
                    flat
                    round
                    icon="delete"
                    title="Slette fil"
                    @click="deleteFile(file)"
                    :loading="deletingFile"
                  ></q-btn
                ></q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-section
              v-if="!selectedResource!.id"
              class="q-py-sm text-caption text-grey-7"
              >Filer kan legges til når vakttypen er lagret.</q-card-section
            >
            <q-card-actions v-if="selectedResource!.id" align="right">
              <q-btn
                icon="add"
                label="Legg til fil"
                no-caps
                flat
                color="primary"
                @click="showAddFile"
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
            @click="saveResource"
            no-caps
          ></q-btn
        ></q-card-actions>
      </q-card>
      <q-dialog v-model="showingAddTrainer">
        <q-card class="full-width">
          <q-card-section>
            <q-select
              class="col"
              label="Bruker"
              placeholder="Velg bruker"
              autofocus
              outlined
              :options="availableTrainerUsers"
              option-label="fullName"
              option-value="id"
              v-model="user"
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
            </q-select></q-card-section
          >
          <q-card-actions align="right">
            <q-btn
              label="Avbryt"
              no-caps
              flat
              @click="showingAddTrainer = false"
            ></q-btn>
            <q-btn
              label="Legg til"
              no-caps
              unelevated
              color="primary"
              @click="addTrainer"
              :disable="!canAddTrainer"
            ></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
      <q-dialog v-model="showingAddFile">
        <q-card class="full-width">
          <q-card-section class="row q-gutter-sm">
            <q-file
              class="col-12"
              label="Fil"
              placeholder="Velg fil"
              autofocus
              outlined
              clearable
              v-model="fileInfo!.file"
              accept="application/pdf, .jpg, .jpeg, .gif, .png"
            >
            </q-file>

            <q-input
              class="col-12"
              label="Beskrivelse"
              outlined
              v-model="fileInfo!.description"
            >
            </q-input
          ></q-card-section>
          <q-card-actions align="right">
            <q-btn
              label="Avbryt"
              no-caps
              flat
              @click="showingAddFile = false"
              :disable="savingFile"
            ></q-btn>
            <q-btn
              label="Legg til"
              no-caps
              unelevated
              color="primary"
              @click="addFile"
              :disable="!canAddFile"
              :loading="savingFile"
            ></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { useCompetencyStore } from "stores/CompetencyStore";
import { downloadResourceTypeFileOrNotify } from "src/shared/fileDownload";
import { getApiErrorMessage } from "src/shared/apiError";
import {
  newEditableTrainer,
  toEditableResourceType,
  toResourceTypeRequest,
  type EditableResourceType,
  type EditableTrainer,
} from "src/shared/resourceTypeForm";
import { computed } from "vue";
import type {
  CompetencyResponse,
  FileInfoResponse,
  ResourceTypeCompetencyResponse,
  ResourceTypeResponse,
  UserResponse,
} from "src/types";

interface FileForm {
  file: File | null;
  description: string | null;
}

const emit = defineEmits<{ "toggle-right": [] }>();
const $router = useRouter();
const eventStore = useEventStore();
const userStore = useUserStore();
const competencyStore = useCompetencyStore();
const $q = useQuasar();

const showingEdit = ref(false);
const user = ref<UserResponse | null>(null);
const loading = ref(false);

onMounted(async () => {
  try {
    loading.value = true;
    await Promise.all([
      userStore.getUsers(),
      eventStore.getResourceTypes(),
      competencyStore.getCompetencies(),
    ]);
  } catch {
  } finally {
    loading.value = false;
  }
});

// Brukere som ikke allerede er (synlige) opplæringsansvarlige.
const availableTrainerUsers = computed(() => {
  const trainerUserIds = visibleTrainers.value.map((t) => t.userId);
  return userStore.users.filter((u) => !trainerUserIds.includes(u.id));
});

const visibleTrainers = computed(() =>
  // Evalueres kun fra redigeringsdialogen, når selectedResource er satt.
  selectedResource.value!.trainers.filter((t) => !t.isDeleted)
);

// null til redigeringsdialogen åpnes; malen bruker `selectedResource!` fordi
// dialoginnholdet kun rendres når den er satt.
const selectedResource = ref<EditableResourceType | null>(null);
function newResourceType() {
  selectedResource.value = emptyResource();
  competencyRequirements.value = [];
  selectedCompetency.value = null;
  showingEdit.value = true;
}

async function editResourceType(resourceType: ResourceTypeResponse) {
  // Kopi på alle nivåer, så en avbrutt dialog ikke endrer trenere/filer i
  // store-staten.
  selectedResource.value = toEditableResourceType(resourceType);
  competencyRequirements.value = [];
  selectedCompetency.value = null;
  showingEdit.value = true;
  await loadCompetencyRequirements(resourceType.id);
}

async function saveResource() {
  try {
    // Kalles kun fra redigeringsdialogen, så selectedResource er satt.
    const resource = selectedResource.value!;
    const request = toResourceTypeRequest(resource);
    let resourceTypeId: number | undefined;
    if (resource.id) {
      await eventStore.updateResourceType({ ...request, id: resource.id });
      resourceTypeId = resource.id;
    } else {
      const created = await eventStore.createResourceType(request);
      resourceTypeId = created?.id;
    }
    if (resourceTypeId) {
      const requirements = competencyRequirements.value.map((cr) => ({
        competencyId: cr.competencyId,
        minimumRequired: cr.minimumRequired,
      }));
      await competencyStore.setResourceTypeCompetencies(
        resourceTypeId,
        requirements
      );
    }
    showingEdit.value = false;
    $q.notify({ message: "Vakttypen er lagret." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å lagre vakttypen." });
  }
}

function confirmDeleteResourceType() {
  // Slett-knappen vises kun når id er satt.
  const resource = selectedResource.value!;
  $q.dialog({
    title: "Slette vakttype",
    message: `Vil du slette vakttypen «${resource.name ?? ""}»?`,
    cancel: { label: "Avbryt", flat: true, noCaps: true },
    ok: { label: "Slett", noCaps: true, color: "negative", unelevated: true },
    persistent: true,
  }).onOk(async () => {
    await deleteResourceType(resource.id!);
  });
}

async function deleteResourceType(id: number) {
  try {
    await eventStore.deleteResourceType({ id });
    showingEdit.value = false;
    $q.notify({ message: "Vakttypen er slettet." });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å slette vakttypen."),
    });
  }
}

function emptyResource(): EditableResourceType {
  return {
    id: null,
    name: null,
    defaultStaff: 1,
    notificationMessage: null,
    trainers: [],
    files: [],
  };
}

function addTrainer() {
  // Knappen er deaktivert til en bruker er valgt (canAddTrainer), og
  // dialogen ligger i redigeringsdialogen.
  const selectedUser = user.value!;
  selectedResource.value!.trainers.push(newEditableTrainer(selectedUser));
  user.value = null;
  showingAddTrainer.value = false;
}

const showingAddTrainer = ref(false);
function showAddTrainer() {
  user.value = null;
  showingAddTrainer.value = true;
}

const canAddTrainer = computed(() => !!user.value);

function deleteTrainer(trainer: EditableTrainer) {
  trainer.isDeleted = true;
}

const competencyRequirements = ref<ResourceTypeCompetencyResponse[]>([]);
const selectedCompetency = ref<CompetencyResponse | null>(null);

const availableCompetencies = computed(() => {
  const linkedIds = competencyRequirements.value.map((cr) => cr.competencyId);
  return competencyStore.competencies.filter((c) => !linkedIds.includes(c.id));
});

function addCompetencyRequirement() {
  // Knappen er deaktivert til en kompetanse er valgt.
  const competency = selectedCompetency.value!;
  competencyRequirements.value.push({
    competencyId: competency.id,
    competencyName: competency.name,
    minimumRequired: 1,
  });
  selectedCompetency.value = null;
}

function removeCompetencyRequirement(index: number) {
  competencyRequirements.value.splice(index, 1);
}

async function loadCompetencyRequirements(resourceTypeId: number) {
  const data =
    await competencyStore.getResourceTypeCompetencies(resourceTypeId);
  // `name` finnes ikke i DTO-en (competencyName er påkrevd); fallbacken er
  // beholdt fra JS-versjonen.
  competencyRequirements.value = data.map(
    (item: ResourceTypeCompetencyResponse & { name?: string }) => ({
      competencyId: item.competencyId,
      competencyName: item.competencyName ?? item.name,
      minimumRequired: item.minimumRequired,
    })
  );
}

// null til fildialogen åpnes; malen bruker `fileInfo!` av samme grunn som
// selectedResource.
const fileInfo = ref<FileForm | null>(null);
const showingAddFile = ref(false);
function showAddFile() {
  fileInfo.value = { file: null, description: null };
  showingAddFile.value = true;
}

const canAddFile = computed(
  // Evalueres kun fra fildialogen, når fileInfo er satt.
  () => !!(fileInfo.value!.file && fileInfo.value!.description)
);

const savingFile = ref(false);
async function addFile() {
  // «Legg til fil» vises kun for en lagret vakttype (id satt).
  const resource = selectedResource.value!;
  if (!resource.id) return;
  try {
    savingFile.value = true;
    const response = await eventStore.addResourceTypeFile(
      { id: resource.id },
      // Knappen er deaktivert til både fil og beskrivelse er satt (canAddFile).
      fileInfo.value as { file: Blob; description: string }
    );
    resource.files = [...(resource.files ?? []), response];
    showingAddFile.value = false;
    $q.notify({ message: "Filen er lagret." });
  } catch (error) {
    $q.notify({ message: "Klarte ikke å lagre filen." });
    console.log(error);
  } finally {
    savingFile.value = false;
  }
}

const deletingFile = ref(false);
async function deleteFile(fileInfo: FileInfoResponse) {
  try {
    deletingFile.value = true;
    await eventStore.deleteResourceTypeFile(fileInfo);
    // Kalles kun fra fillista, så selectedResource og files er satt.
    const resource = selectedResource.value!;
    resource.files = (resource.files ?? []).filter((f) => f.id !== fileInfo.id);
    showingAddFile.value = false;
    $q.notify({ message: "Filen er slettet." });
  } catch (error) {
    console.log(error);
    $q.notify({ message: "Klarte ikke å slette filen." });
  } finally {
    deletingFile.value = false;
  }
}
</script>
