<template>
  <q-input
    outlined
    inputmode="numeric"
    hide-bottom-space
    mask="##.##.####"
    placeholder="DD.MM.ÅÅÅÅ"
    :readonly="props.readonly"
    @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
    :model-value="props.modelValue"
    @update:model-value="
      (val) => emit('update:model-value', val as string | null)
    "
    ><template v-slot:append v-if="!props.readonly">
      <q-icon name="event" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-date
            :model-value="props.modelValue"
            @update:model-value="(val) => emit('update:model-value', val)"
            :mask="QUASAR_DATE_MASK"
            today-btn
          >
            <div class="row items-center justify-end">
              <q-btn v-close-popup label="Lukk" color="primary" flat />
            </div>
          </q-date>
        </q-popup-proxy>
      </q-icon> </template
  ></q-input>
</template>

<script setup lang="ts">
import { QUASAR_DATE_MASK } from "@/shared/time";

// Wrapper rundt q-input; QInput typer verdien som string | number | null,
// men uten type="number" sender den aldri number.
const props = withDefaults(
  defineProps<{
    modelValue?: string | null | undefined;
    readonly?: boolean;
  }>(),
  {
    modelValue: undefined,
    readonly: false,
  }
);
const emit = defineEmits<{
  "update:model-value": [value: string | null];
}>();
</script>
