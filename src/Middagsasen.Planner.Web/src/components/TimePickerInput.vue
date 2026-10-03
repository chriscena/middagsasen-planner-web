<template>
  <q-input
    outlined
    mask="##:##"
    placeholder="TT:MM"
    inputmode="numeric"
    hide-bottom-space
    :readonly="props.readonly"
    :model-value="props.modelValue"
    @update:model-value="(val) => emit('update:model-value', val as string | null)"
    @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
  >
    <template v-slot:append v-if="!props.readonly">
      <q-icon name="access_time" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-time
            :model-value="props.modelValue"
            @update:model-value="(val) => emit('update:model-value', val)"
            format24h
            :mask="QUASAR_TIME_MASK"
            now-btn
          >
            <div class="row items-center justify-end">
              <q-btn v-close-popup label="Lukk" color="primary" flat />
            </div>
          </q-time>
        </q-popup-proxy>
      </q-icon> </template
  ></q-input>
</template>

<script setup lang="ts">
import { QUASAR_TIME_MASK } from "src/shared/time";

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
