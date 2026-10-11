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
        <ProfileMenuButton @click="emit('toggle-right')"></ProfileMenuButton>
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
            >{{ resourceType.defaultShiftCount }}
            {{ resourceType.defaultShiftCount === 1 ? "vakt" : "vakter" }}
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
            label="Standard antall vakter"
            v-model="selectedResource!.defaultShiftCount"
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
            <q-card-section class="q-py-sm text-subtitle2"
              >Vedlegg</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item v-for="file in selectedResource!.files" :key="file.id">
                <q-item-section>
                  <q-item-label>{{ file.description }}</q-item-label>
                  <q-item-label caption>{{ file.fileName }}</q-item-label>
                  <q-item-label v-if="file.requiresAcknowledgement" caption>
                    <q-badge color="primary" label="Instruks"></q-badge>
                    <q-badge
                      v-if="file.isMandatory"
                      outline
                      color="grey-8"
                      label="Påkrevd"
                      class="q-ml-xs"
                    ></q-badge>
                  </q-item-label>
                  <!-- Teller fra kvitteringsrapporten (all=false). Klikk åpner
                       rapporten; mens tellingen hentes vises en nøytral
                       tekst, og feiler den kan rapporten likevel åpnes. -->
                  <q-item-label v-if="file.requiresAcknowledgement" caption>
                    <q-btn
                      flat
                      dense
                      no-caps
                      size="sm"
                      padding="none"
                      color="primary"
                      :label="acknowledgementCountLabel(file)"
                      title="Vis kvitteringer"
                      @click="showReport(file)"
                    ></q-btn>
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <div class="row no-wrap">
                    <q-btn
                      flat
                      round
                      icon="download"
                      title="Last ned vedlegg"
                      @click="downloadResourceTypeFileOrNotify(file)"
                    ></q-btn>
                    <q-btn
                      flat
                      round
                      icon="edit"
                      title="Endre vedlegg"
                      @click="showEditFile(file)"
                    ></q-btn>
                    <q-btn
                      flat
                      round
                      icon="archive"
                      title="Arkiver vedlegg"
                      @click="confirmArchiveFile(file)"
                      :loading="archivingFile"
                    ></q-btn>
                  </div>
                </q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-section
              v-if="!selectedResource!.id"
              class="q-py-sm text-caption text-grey-7"
              >Vedlegg kan legges til når vakttypen er lagret.</q-card-section
            >
            <q-card-actions v-if="selectedResource!.id" align="right">
              <q-btn
                icon="add"
                label="Legg til vedlegg"
                no-caps
                flat
                color="primary"
                @click="showAddFile"
              ></q-btn>
            </q-card-actions>
            <template v-if="selectedResource!.id">
              <q-separator></q-separator>
              <!-- Hentes første gang seksjonen åpnes; nullstilles ved bytte
                   av vakttype og etter arkivering. -->
              <q-expansion-item
                v-model="showingArchived"
                label="Arkiverte vedlegg"
                icon="inventory_2"
                dense
                @show="ensureArchivedFiles"
              >
                <div v-if="archivedLoading" class="flex flex-center q-pa-md">
                  <q-spinner color="primary" size="2em"></q-spinner>
                </div>
                <div
                  v-else-if="archivedFailed"
                  class="row items-center q-pa-md q-gutter-sm"
                >
                  <span class="text-negative"
                    >Klarte ikke å hente arkiverte vedlegg.</span
                  >
                  <q-btn
                    flat
                    dense
                    no-caps
                    color="primary"
                    icon="refresh"
                    label="Prøv igjen"
                    @click="loadArchivedFiles"
                  ></q-btn>
                </div>
                <div
                  v-else-if="archivedFiles && archivedFiles.length === 0"
                  class="q-pa-md text-caption text-grey-7"
                >
                  Ingen arkiverte vedlegg
                </div>
                <q-list v-else-if="archivedFiles" role="list" separator>
                  <q-item v-for="file in archivedFiles" :key="file.id">
                    <q-item-section>
                      <q-item-label>{{ file.description }}</q-item-label>
                      <q-item-label caption>{{ file.fileName }}</q-item-label>
                      <q-item-label caption
                        >Arkivert {{ formatDate(file.archived) }}</q-item-label
                      >
                    </q-item-section>
                    <q-item-section side>
                      <div class="row no-wrap">
                        <q-btn
                          v-if="file.requiresAcknowledgement"
                          flat
                          round
                          icon="fact_check"
                          title="Vis kvitteringer"
                          @click="showReport(file)"
                        ></q-btn>
                        <q-btn
                          flat
                          round
                          icon="download"
                          title="Last ned vedlegg"
                          @click="downloadResourceTypeFileOrNotify(file)"
                        ></q-btn>
                      </div>
                    </q-item-section>
                  </q-item>
                </q-list>
              </q-expansion-item>
            </template>
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
      <AcknowledgementReportDialog
        v-if="reportFile"
        v-model="showingReport"
        :file="reportFile"
        :resource-type-id="reportFile.resourceTypeId"
      ></AcknowledgementReportDialog>
      <!-- Samme dialog for nytt vedlegg og redigering av metadata. Selve
           filen kan ikke byttes (kvitteringene gjelder innholdet); nytt
           innhold er arkiver + legg til. -->
      <q-dialog v-model="showingAddFile">
        <q-card class="full-width">
          <q-card-section class="text-h6">{{
            editingFile ? "Endre vedlegg" : "Nytt vedlegg"
          }}</q-card-section>
          <q-card-section class="row q-gutter-sm">
            <q-file
              v-if="!editingFile"
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
              :autofocus="!!editingFile"
              v-model="fileInfo!.description"
            >
            </q-input>
            <q-toggle
              class="col-12"
              label="Krever kvittering"
              v-model="fileInfo!.requiresAcknowledgement"
              @update:model-value="onRequiresAcknowledgementChanged"
            ></q-toggle>
            <!-- Påkrevd forutsetter kvittering; nullstilles når den slås av. -->
            <q-toggle
              class="col-12"
              label="Påkrevd for å ta vakt"
              v-model="fileInfo!.isMandatory"
              :disable="!fileInfo!.requiresAcknowledgement"
            ></q-toggle
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
              :label="editingFile ? 'Lagre' : 'Legg til'"
              no-caps
              unelevated
              color="primary"
              @click="saveFile"
              :disable="!canSaveFile"
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
import AcknowledgementReportDialog from "@/components/AcknowledgementReportDialog.vue";
import ProfileMenuButton from "@/components/ProfileMenuButton.vue";
import { useEventStore } from "@/stores/EventStore";
import { useUserStore } from "@/stores/UserStore";
import { useCompetencyStore } from "@/stores/CompetencyStore";
import { downloadResourceTypeFileOrNotify } from "@/shared/fileDownload";
import { createLatest } from "@/shared/latestRequest";
import { notifyApiError } from "@/shared/notifyApiError";
import { formatDate } from "@/shared/time";
import {
  newEditableTrainer,
  toEditableResourceType,
  toResourceTypeRequest,
  type EditableResourceType,
  type EditableTrainer,
} from "@/shared/resourceTypeForm";
import { computed } from "vue";
import type {
  CompetencyResponse,
  FileInfoResponse,
  FileMetadataRequest,
  ResourceTypeCompetencyResponse,
  ResourceTypeResponse,
  UserResponse,
} from "@/types";

// Skjemaet i vedleggsdialogen. file er null i redigeringsmodus (filen kan
// ikke byttes).
interface FileForm {
  file: File | null;
  description: string | null;
  requiresAcknowledgement: boolean;
  isMandatory: boolean;
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
  } catch (error) {
    notifyApiError(error, "Klarte ikke å hente vakttyper og kompetanser.");
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
  resetFileReports();
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
  resetFileReports();
  void loadAcknowledgementCounts();
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
    notifyApiError(error, "Klarte ikke å lagre vakttypen.");
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
    notifyApiError(error, "Klarte ikke å slette vakttypen.");
  }
}

function emptyResource(): EditableResourceType {
  return {
    id: null,
    name: null,
    defaultShiftCount: 1,
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
// Vedlegget som redigeres; null = nytt vedlegg.
const editingFile = ref<FileInfoResponse | null>(null);
function showAddFile() {
  editingFile.value = null;
  fileInfo.value = {
    file: null,
    description: null,
    requiresAcknowledgement: false,
    isMandatory: false,
  };
  showingAddFile.value = true;
}

function showEditFile(file: FileInfoResponse) {
  editingFile.value = file;
  fileInfo.value = {
    file: null,
    description: file.description,
    requiresAcknowledgement: file.requiresAcknowledgement,
    isMandatory: file.isMandatory,
  };
  showingAddFile.value = true;
}

// Påkrevd kan bare være sant når krever kvittering er sant.
function onRequiresAcknowledgementChanged(value: boolean) {
  if (!value) fileInfo.value!.isMandatory = false;
}

const canSaveFile = computed(() => {
  // Evalueres kun fra vedleggsdialogen, når fileInfo er satt.
  const form = fileInfo.value!;
  return !!form.description?.trim() && (!!editingFile.value || !!form.file);
});

const savingFile = ref(false);
async function saveFile() {
  // «Legg til vedlegg» vises kun for en lagret vakttype (id satt).
  const resource = selectedResource.value!;
  if (!resource.id) return;
  // Knappen er deaktivert til beskrivelse (og fil for nytt vedlegg) er satt.
  const form = fileInfo.value!;
  const metadata: FileMetadataRequest = {
    description: form.description!.trim(),
    requiresAcknowledgement: form.requiresAcknowledgement,
    isMandatory: form.requiresAcknowledgement && form.isMandatory,
  };
  try {
    savingFile.value = true;
    if (editingFile.value) {
      const updated = await eventStore.updateResourceTypeFile(
        resource.id,
        editingFile.value.id,
        metadata
      );
      resource.files = (resource.files ?? []).map((f) =>
        f.id === updated.id ? updated : f
      );
    } else {
      const created = await eventStore.addResourceTypeFile(
        { id: resource.id },
        { ...metadata, file: form.file! }
      );
      resource.files = [...(resource.files ?? []), created];
    }
    showingAddFile.value = false;
    // Flagget kan være endret, og et nytt vedlegg trenger teller.
    if (selectedResource.value === resource) void loadAcknowledgementCounts();
    $q.notify({ message: "Vedlegget er lagret." });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å lagre vedlegget.");
  } finally {
    savingFile.value = false;
  }
}

function confirmArchiveFile(file: FileInfoResponse) {
  $q.dialog({
    title: "Arkivere vedlegg",
    message:
      "Arkivere vedlegget? Det fjernes fra vakttypen, men kvitteringene beholdes.",
    cancel: { label: "Avbryt", flat: true, noCaps: true },
    ok: { label: "Arkiver", noCaps: true, color: "primary", unelevated: true },
    persistent: true,
  }).onOk(async () => {
    await archiveFile(file);
  });
}

const archivingFile = ref(false);
// DELETE arkiverer i backend; vedlegget slettes aldri fysisk.
async function archiveFile(file: FileInfoResponse) {
  try {
    archivingFile.value = true;
    await eventStore.deleteResourceTypeFile(file);
    // Kalles kun fra vedleggslista, så selectedResource og files er satt.
    const resource = selectedResource.value!;
    resource.files = (resource.files ?? []).filter((f) => f.id !== file.id);
    $q.notify({ message: "Vedlegget er arkivert." });
    void loadAcknowledgementCounts();
    invalidateArchivedFiles();
  } catch (error) {
    notifyApiError(error, "Klarte ikke å arkivere vedlegget.");
  } finally {
    archivingFile.value = false;
  }
}

// ---------------------------------------------------------------------------
// Kvitteringer og arkiv (admin). Tellerne og arkivlisten gjelder vakttypen i
// selectedResource. Hver henting får et løpenummer; svar for en vakttype som
// ikke lenger er valgt (eller fra en eldre henting) forkastes.

// Kvittert/totalt per aktiv instruks (fil-id). Mangler = hentes; null =
// hentingen feilet.
interface AcknowledgementCount {
  acknowledgedCount: number;
  total: number;
}
const acknowledgementCounts = ref<Record<number, AcknowledgementCount | null>>(
  {}
);
const latestCounts = createLatest();

// Henter tellerne for alle aktive instrukser på vakttypen i ett kall.
// Tellerne tømmes ikke her: ved bytte av vakttype gjør resetFileReports det,
// og etter lagring/arkivering på samme vakttype står de gamle tallene til de
// nye kommer (unngår flimmer). Et nytt vedlegg mangler i tellerne og viser
// «Henter kvitteringer …» til svaret er der.
async function loadAcknowledgementCounts() {
  const token = latestCounts.start();
  const resource = selectedResource.value;
  const resourceTypeId = resource?.id;
  if (!resource || !resourceTypeId) return;
  const files = (resource.files ?? []).filter((f) => f.requiresAcknowledgement);
  if (files.length === 0) {
    acknowledgementCounts.value = {};
    return;
  }
  try {
    const summaries =
      await eventStore.getAcknowledgementSummaries(resourceTypeId);
    if (!latestCounts.isCurrent(token)) return;
    const byFileId = new Map(summaries.map((s) => [s.fileId, s]));
    // En instruks som mangler i svaret (bør ikke skje) får null, så den
    // viser «Vis kvitteringer» og ikke «Henter …» for alltid.
    const counts: Record<number, AcknowledgementCount | null> = {};
    for (const file of files) {
      const summary = byFileId.get(file.id);
      counts[file.id] = summary
        ? {
            acknowledgedCount: summary.acknowledgedCount,
            total: summary.total,
          }
        : null;
    }
    acknowledgementCounts.value = counts;
  } catch (error) {
    if (!latestCounts.isCurrent(token)) return;
    acknowledgementCounts.value = Object.fromEntries(
      files.map((f) => [f.id, null])
    );
    notifyApiError(error, "Klarte ikke å hente kvitteringene.");
  }
}

function acknowledgementCountLabel(file: FileInfoResponse): string {
  if (!(file.id in acknowledgementCounts.value)) {
    return "Henter kvitteringer …";
  }
  const count = acknowledgementCounts.value[file.id];
  if (!count) return "Vis kvitteringer";
  return `${count.acknowledgedCount} av ${count.total} har kvittert`;
}

// Arkiverte vedlegg: null = ikke hentet (eller ugyldiggjort).
const archivedFiles = ref<FileInfoResponse[] | null>(null);
const archivedLoading = ref(false);
// Siste henting av arkivlisten feilet; vises med «Prøv igjen».
const archivedFailed = ref(false);
const showingArchived = ref(false);
const latestArchived = createLatest();

async function loadArchivedFiles() {
  const resourceTypeId = selectedResource.value?.id;
  if (!resourceTypeId) return;
  const token = latestArchived.start();
  archivedLoading.value = true;
  archivedFailed.value = false;
  try {
    const files = await eventStore.getArchivedResourceTypeFiles(resourceTypeId);
    if (latestArchived.isCurrent(token)) archivedFiles.value = files;
  } catch (error) {
    if (latestArchived.isCurrent(token)) {
      archivedFailed.value = true;
      notifyApiError(error, "Klarte ikke å hente arkiverte vedlegg.");
    }
  } finally {
    if (latestArchived.isCurrent(token)) archivedLoading.value = false;
  }
}

// Kalles når seksjonen åpnes: hent bare hvis listen ikke alt er hentet
// (eller feilet forrige gang).
function ensureArchivedFiles() {
  if (archivedFiles.value === null && !archivedLoading.value) {
    void loadArchivedFiles();
  }
}

// Etter arkivering er listen utdatert. Er seksjonen åpen, hentes den på
// nytt med en gang, ellers ved neste åpning.
function invalidateArchivedFiles() {
  latestArchived.cancel();
  archivedFiles.value = null;
  archivedLoading.value = false;
  archivedFailed.value = false;
  if (showingArchived.value) void loadArchivedFiles();
}

// Ved bytte av vakttype: forkast tellere, arkivliste og svar underveis, og
// lukk arkivseksjonen.
function resetFileReports() {
  latestCounts.cancel();
  acknowledgementCounts.value = {};
  latestArchived.cancel();
  archivedFiles.value = null;
  archivedLoading.value = false;
  archivedFailed.value = false;
  showingArchived.value = false;
}

// Kvitteringsrapporten deler én dialog for aktive og arkiverte instrukser.
const reportFile = ref<FileInfoResponse | null>(null);
const showingReport = ref(false);
function showReport(file: FileInfoResponse) {
  reportFile.value = file;
  showingReport.value = true;
}
</script>
