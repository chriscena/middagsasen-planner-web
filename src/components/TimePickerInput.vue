<template>
  <q-input
    outlined
    mask="##:##"
    placeholder="TT:MM"
    inputmode="numeric"
    hide-bottom-space
    :model-value="props.modelValue"
    @update:model-value="(val) => emit('update:model-value', val)"
    @focus="(event) => (event.target?.select ? event.target.select() : _)"
  >
    <template v-slot:append>
      <q-icon name="access_time" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-time
            :model-value="props.modelValue"
            @update:model-value="(val) => emit('update:model-value', val)"
            format24h
            mask="HH:mm"
          >
            <div class="row items-center justify-end">
              <q-btn v-close-popup label="Lukk" color="primary" flat />
            </div>
          </q-time>
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
