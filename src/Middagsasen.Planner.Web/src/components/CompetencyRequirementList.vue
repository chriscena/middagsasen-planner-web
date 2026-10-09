<template>
  <q-card bordered flat>
    <q-card-section class="q-py-sm">
      <div class="text-subtitle2">Anleggskrav</div>
      <div class="text-caption text-grey-8">
        Minst så mange på vakt skal ha kompetansen hele åpningstiden
      </div>
    </q-card-section>
    <q-separator></q-separator>
    <q-list separator>
      <q-item v-if="!props.modelValue.length">
        <q-item-section>
          <q-item-label>Ingen anleggskrav</q-item-label>
        </q-item-section>
      </q-item>
      <q-item
        v-for="requirement in props.modelValue"
        :key="requirement.clientKey"
        class="q-py-sm"
      >
        <q-item-section>
          <div class="row q-col-gutter-sm items-start no-wrap">
            <q-select
              class="col"
              outlined
              dense
              label="Kompetanse"
              :options="optionsFor(requirement)"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              :loading="loadingCompetencies"
              :model-value="requirement.competencyId"
              :error="!!errors[requirement.clientKey]?.competency"
              :error-message="errors[requirement.clientKey]?.competency ?? ''"
              no-option-label="Ingen flere kompetanser"
              @update:model-value="
                (value: number | null) =>
                  update(requirement, {
                    competencyId: value,
                    competencyName: null,
                  })
              "
            ></q-select>
            <q-input
              style="width: 90px"
              outlined
              dense
              type="number"
              min="1"
              step="1"
              label="Antall"
              :model-value="requirement.minimumRequired"
              :error="!!errors[requirement.clientKey]?.minimumRequired"
              :error-message="
                errors[requirement.clientKey]?.minimumRequired ?? ''
              "
              @update:model-value="
                (value: string | number | null) =>
                  update(requirement, { minimumRequired: value })
              "
            ></q-input>
            <div>
              <q-btn
                flat
                round
                icon="delete"
                title="Fjern anleggskrav"
                aria-label="Fjern anleggskrav"
                @click="remove(requirement)"
              ></q-btn>
            </div>
          </div>
        </q-item-section>
      </q-item>
    </q-list>
    <q-separator></q-separator>
    <q-card-actions align="right">
      <q-btn
        no-caps
        dense
        flat
        label="Legg til anleggskrav"
        color="primary"
        icon="add"
        @click="add"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useCompetencyStore } from "@/stores/CompetencyStore";
import { notifyApiError } from "@/shared/notifyApiError";
import {
  competencyOptionsFor,
  competencyRequirementErrors,
  newCompetencyRequirementDraft,
  type CompetencyOption,
  type CompetencyRequirementDraft,
} from "@/shared/competencyRequirements";

// Anleggskravene i vaktliste- og malskjemaet (EventForm/TemplateForm).
// Forelderen eier lista og avgjør om den kan lagres
// (areValidCompetencyRequirements); feilene vises her per felt.
const props = withDefaults(
  defineProps<{
    modelValue?: CompetencyRequirementDraft[];
  }>(),
  {
    modelValue: () => [],
  }
);

const emit = defineEmits<{
  "update:model-value": [value: CompetencyRequirementDraft[]];
}>();

const competencyStore = useCompetencyStore();
const loadingCompetencies = ref(false);

onMounted(async () => {
  // Kompetansene er ofte alt hentet (høyremenyen i MainLayout).
  if (competencyStore.competencies.length) return;
  try {
    loadingCompetencies.value = true;
    await competencyStore.getCompetencies();
  } catch (error) {
    notifyApiError(error, "Klarte ikke å hente kompetanser.");
  } finally {
    loadingCompetencies.value = false;
  }
});

const errors = computed(() => competencyRequirementErrors(props.modelValue));

function optionsFor(
  requirement: CompetencyRequirementDraft
): CompetencyOption[] {
  return competencyOptionsFor(
    requirement,
    props.modelValue,
    competencyStore.competencies
  );
}

function add() {
  emit("update:model-value", [
    ...props.modelValue,
    newCompetencyRequirementDraft(),
  ]);
}

function update(
  requirement: CompetencyRequirementDraft,
  changes: Partial<
    Pick<
      CompetencyRequirementDraft,
      "competencyId" | "competencyName" | "minimumRequired"
    >
  >
) {
  emit(
    "update:model-value",
    props.modelValue.map((r) =>
      r.clientKey === requirement.clientKey ? { ...r, ...changes } : r
    )
  );
}

function remove(requirement: CompetencyRequirementDraft) {
  emit(
    "update:model-value",
    props.modelValue.filter((r) => r.clientKey !== requirement.clientKey)
  );
}
</script>
