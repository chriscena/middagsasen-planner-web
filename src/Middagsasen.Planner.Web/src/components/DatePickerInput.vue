<template>
  <q-input
    outlined
    inputmode="numeric"
    hide-bottom-space
    mask="##.##.####"
    placeholder="DD.MM.ÅÅÅÅ"
    @focus="(event) => (event.target?.select ? event.target.select() : _)"
    :model-value="props.modelValue"
    @update:model-value="(val) => emit('update:model-value', val)"
    ><template v-slot:append>
      <q-icon name="event" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-date
            :model-value="props.modelValue"
            @update:model-value="(val) => emit('update:model-value', val)"
            mask="DD.MM.YYYY"
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

<script setup>
const props = defineProps({
  modelValue: {
    type: String,
    default: undefined,
  },
});
const emit = defineEmits(["update:model-value"]);
</script>
