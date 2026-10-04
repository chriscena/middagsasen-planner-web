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
            {{
              (competency.resourceTypes?.length || 0) === 1
                ? "vakttype"
                : "vakttyper"
            }}
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
                :key="approver.userId"
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
                      <q-item-label caption>{{
                        scope.opt.phoneNo
                      }}</q-item-label>
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

<script setup lang="ts">
import { onMounted, ref, computed } from "vue";
import { useRouter } from "vue-router";
import { useQuasar } from "quasar";
import { useCompetencyStore } from "@/stores/CompetencyStore";
import { useUserStore } from "@/stores/UserStore";
import { notifyApiError } from "@/shared/notifyApiError";
import { applyApproverResults, localApprovers } from "@/shared/approvers";
import type {
  CompetencyApproverResponse,
  CompetencyRequest,
  CompetencyResponse,
  UserResponse,
} from "@/types";

// Skjemaet i redigeringsdialogen.
interface CompetencyForm {
  id: number | null;
  name: string | null;
  description: string | null | undefined;
  hasExpiry: boolean;
  approvers: CompetencyApproverResponse[];
}

const emit = defineEmits<{ "toggle-right": [] }>();
const $router = useRouter();
const competencyStore = useCompetencyStore();
const userStore = useUserStore();
const $q = useQuasar();

const showingEdit = ref(false);
const loading = ref(false);
const selected = ref<CompetencyForm>(emptyCompetency());
const selectedApprover = ref<UserResponse | null>(null);

onMounted(async () => {
  try {
    loading.value = true;
    await Promise.all([
      competencyStore.getCompetencies(),
      userStore.getUsers(),
    ]);
  } catch (error) {
    notifyApiError(error, "Klarte ikke å hente kompetanser og brukere.");
  } finally {
    loading.value = false;
  }
});

const availableApprovers = computed(() => {
  const approverUserIds = selected.value.approvers.map((a) => a.userId);
  return userStore.users.filter((u) => !approverUserIds.includes(u.id));
});

function emptyCompetency(): CompetencyForm {
  return {
    id: null,
    name: null,
    description: null,
    hasExpiry: false,
    approvers: [],
  };
}

function newCompetency(): void {
  selected.value = emptyCompetency();
  showingEdit.value = true;
}

async function editCompetency(competency: CompetencyResponse): Promise<void> {
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
    notifyApiError(error, "Klarte ikke å hente kompetanse.");
  }
}

async function saveCompetency(): Promise<void> {
  // Cast: name kan være null og description undefined her; backend avviser
  // manglende navn med 400 (som før).
  const request = {
    name: selected.value.name,
    description: selected.value.description,
    hasExpiry: selected.value.hasExpiry,
  } as CompetencyRequest;
  try {
    let id = selected.value.id;
    if (id) {
      await competencyStore.updateCompetency(id, request);
    } else {
      const created = await competencyStore.createCompetency(request);
      id = created.id;
      // Settes med en gang, slik at et nytt «Lagre» oppdaterer i stedet for å
      // opprette kompetansen på nytt.
      selected.value.id = id;
    }
    // Godkjennere lagt til før kompetansen fantes, eller som feilet ved et
    // tidligere forsøk, ligger kun lokalt (id 0). CompetencyRequest har ikke
    // approvers, så de legges til etter lagring.
    const pending = localApprovers(selected.value.approvers);
    if (pending.length) {
      const competencyId = id;
      const results = await Promise.allSettled(
        pending.map((a) => competencyStore.addApprover(competencyId, a.userId))
      );
      const { approvers, failed } = applyApproverResults(
        selected.value.approvers,
        pending,
        results
      );
      selected.value.approvers = approvers;
      if (failed > 0) {
        results.forEach((r) => {
          if (r.status === "rejected") console.log(r.reason);
        });
        // Dialogen står åpen, så de feilede godkjennerne ikke går tapt.
        $q.notify({
          message: `Kompetansen er lagret, men ${failed} av ${pending.length} godkjennere kunne ikke legges til. Trykk «Lagre» for å prøve igjen.`,
        });
        return;
      }
    }
    showingEdit.value = false;
    $q.notify({ message: "Kompetansen er lagret." });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å lagre kompetansen.");
  }
}

async function deleteCompetency(): Promise<void> {
  try {
    // Slett-knappen vises kun når id er satt.
    await competencyStore.deleteCompetency(selected.value.id!);
    showingEdit.value = false;
    $q.notify({ message: "Kompetansen er slettet." });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å slette kompetansen.");
  }
}

async function addApprover(): Promise<void> {
  // Legg til-knappen er deaktivert når ingen bruker er valgt.
  const approverUser = selectedApprover.value!;
  if (!selected.value.id) {
    selected.value.approvers.push({
      id: 0,
      userId: approverUser.id,
      // fullName er nullable i UserResponse, men UserService setter den alltid
      // (MapFullName), så `?? ""` er kun for typen.
      fullName: approverUser.fullName ?? "",
    });
    selectedApprover.value = null;
    return;
  }
  try {
    const approver = await competencyStore.addApprover(
      selected.value.id,
      approverUser.id
    );
    selected.value.approvers.push(approver);
    selectedApprover.value = null;
    $q.notify({ message: "Godkjenner er lagt til." });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å legge til godkjenner.");
  }
}

async function removeApprover(
  approver: CompetencyApproverResponse
): Promise<void> {
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
    notifyApiError(error, "Klarte ikke å fjerne godkjenner.");
  }
}
</script>
