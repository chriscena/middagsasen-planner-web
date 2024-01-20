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
            !selectedResource.id ? "Ny vakttype" : "Endre vakttype"
          }}</span
          ><q-space></q-space>
          <q-btn
            v-if="selectedResource.id"
            color="negative"
            round
            flat
            dense
            icon="delete"
          ></q-btn
        ></q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input
            autofocus
            outlined
            label="Navn"
            v-model="selectedResource.name"
          ></q-input>
          <q-input
            outlined
            label="Antall"
            v-model="selectedResource.defaultStaff"
            suffix="stk"
            @focus="(event) => event.target.select()"
          ></q-input>
          <q-card bordered flat>
            <q-card-section class="q-py-sm text-subtitle2"
              >Opplæringsansvarlig</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item v-for="trainer in visibleTrainers" :key="trainer.id">
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
            <q-card-section class="q-py-sm text-subtitle2">Filer</q-card-section
            ><q-separator></q-separator>
            <q-list role="list" separator>
              <q-item v-for="file in selectedResource.files" :key="file.id">
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
                    icon="delete"
                    title="Slette fil"
                    @click="deleteFile(file)"
                  ></q-btn
                ></q-item-section>
              </q-item>
            </q-list>
            <q-separator></q-separator>
            <q-card-actions align="right">
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
              :options="users"
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
              v-model="fileInfo.file"
              accept=".pdf, application/pdf"
            >
            </q-file>

            <q-input
              class="col-12"
              label="Beskrivelse"
              outlined
              v-model="fileInfo.description"
            >
            </q-input
          ></q-card-section>
          <q-card-actions align="right">
            <q-btn
              label="Avbryt"
              no-caps
              flat
              @click="showingAddFile = false"
            ></q-btn>
            <q-btn
              label="Legg til"
              no-caps
              unelevated
              color="primary"
              @click="addFile"
              :disable="!canAddFile"
            ></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { computed } from "vue";

const emit = defineEmits(["toggle-right"]);
const $router = useRouter();
const eventStore = useEventStore();
const userStore = useUserStore();

const showingEdit = ref(false);
const user = ref(null);
const loading = ref(false);

onMounted(async () => {
  try {
    loading.value = true;
    await userStore.getUsers();
    await eventStore.getResourceTypes();
  } catch (error) {
  } finally {
    loading.value = false;
  }
});

const users = computed(() => userStore.users);
const visibleTrainers = computed(() =>
  selectedResource.value.trainers.filter((t) => !t.isDeleted)
);

const selectedResource = ref(null);
function newResourceType() {
  selectedResource.value = emptyResource();
  showingEdit.value = true;
}

function editResourceType(resourceType) {
  selectedResource.value = Object.assign({}, resourceType);
  showingEdit.value = true;
}

function saveResource() {
  if (selectedResource.value.id) {
    eventStore.updateResourceType(selectedResource.value);
  } else {
    eventStore.createResourceType(selectedResource.value);
  }
  showingEdit.value = false;
}

function emptyResource() {
  return {
    id: null,
    name: null,
    defaultStaff: 1,
    trainers: [],
  };
}

function addTrainer() {
  selectedResource.value.trainers.push({
    deleted: false,
    id: 0,
    userId: user.value.id,
    fullName: user.value.fullName,
    phoneNo: user.value.phoneNo,
  });
  user.value = null;
  showingAddTrainer.value = false;
}

const showingAddTrainer = ref(false);
function showAddTrainer() {
  user.value = null;
  showingAddTrainer.value = true;
}

const canAddTrainer = computed(() => !!user.value);

function deleteTrainer(trainer) {
  trainer.isDeleted = true;
}

const fileInfo = ref(null);
const showingAddFile = ref(false);
function showAddFile() {
  fileInfo.value = { file: null, description: null };
  showingAddFile.value = true;
}

const canAddFile = computed(
  () => !!(fileInfo.value.file && fileInfo.value.description)
);

async function addFile() {
  try {
    const response = await eventStore.addResourceTypeFile(
      selectedResource.value,
      fileInfo.value
    );
    selectedResource.value.files.push(response);
    showingAddFile.value = false;
  } catch (error) {
    console.log(error);
  }
}

async function deleteFile(fileInfo) {
  try {
    await eventStore.deleteResourceTypeFile(fileInfo);
    selectedResource.value.files = selectedResource.value.files.filter(
      (f) => f.id !== fileInfo.id
    );
    showingAddFile.value = false;
  } catch (error) {
    console.log(error);
  }
}
</script>
