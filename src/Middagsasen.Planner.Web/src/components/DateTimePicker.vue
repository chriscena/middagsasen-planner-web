<template>
  <q-input
    :model-value="formattedDate"
    @update:model-value="emitDate"
    :label="label ?? undefined"
    :readonly="readonly"
    filled
    mask="##.##.#### ##:##"
    :dense="dense"
    :disable="disable"
  >
    <template #prepend>
      <q-icon name="event" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-date
            :first-day-of-week="1"
            :readonly="readonly"
            :model-value="formattedDate"
            :mask="dateMask"
            @update:model-value="emitDate"
            today-btn
          >
            <div class="row items-center justify-end">
              <q-btn
                v-close-popup
                label="Lukk"
                color="primary"
                flat
              ></q-btn></div
          ></q-date>
        </q-popup-proxy>
      </q-icon>
    </template>

    <template #append>
      <q-icon name="access_time" class="cursor-pointer">
        <q-popup-proxy transition-show="scale" transition-hide="scale">
          <q-time
            :readonly="readonly"
            :model-value="formattedDate"
            :mask="dateMask"
            format24h
            @update:model-value="emitDate"
            now-btn
            :default-date="formattedDefaultDate"
          >
            <div class="row items-center justify-end">
              <q-btn
                v-close-popup
                label="Lukk"
                color="primary"
                flat
              ></q-btn></div
          ></q-time>
        </q-popup-proxy>
      </q-icon>
    </template>
  </q-input>
</template>

<script setup lang="ts">
import { computed, getCurrentInstance, ref, watch } from "vue";
import { format, formatISO, parse } from "date-fns";

const dateMask = "DD.MM.YYYY HH:mm";
const dateFormat = "dd.MM.yyyy HH:mm";

const props = withDefaults(
  defineProps<{
    modelValue?: string | null;
    label?: string | null;
    dense?: boolean;
    disable?: boolean;
    readonly?: boolean;
    status?: number | null;
    disabledDays?: string[];
    defaultDate?: string;
  }>(),
  {
    modelValue: null,
    label: null,
    dense: false,
    disable: false,
    readonly: false,
    status: null,
    disabledDays: () => [],
    defaultDate: "",
  }
);

const emit = defineEmits<{
  "update:modelValue": [value: string | null];
}>();

const instance = getCurrentInstance();

const formattedDefaultDate = computed(() => {
  return props.defaultDate
    ? format(new Date(props.defaultDate), "yyyy/MM/dd")
    : format(new Date(), "yyyy/MM/dd");
});

const formattedDate = computed(() => {
  return props.modelValue
    ? format(new Date(props.modelValue), dateFormat)
    : null;
});

const selectedDate = ref<string | null>(null);

watch(
  () => props.modelValue,
  (newValue, oldValue) => {
    if (!newValue) selectedDate.value = null;
    // new Date(null) === new Date(0); `?? 0` gir samme verdi uten å sende null til Date.
    if (newValue !== oldValue)
      selectedDate.value = format(new Date(newValue ?? 0), dateFormat);
  },
  { immediate: true }
);

function emitDate(value: string | number | null) {
  // OBS (#82): mangler return etter null-emit; parse/formatISO kjøres også for tom verdi.
  if (!value) emit("update:modelValue", null);
  try {
    const defaultDate = props.defaultDate
      ? new Date(props.defaultDate)
      : new Date();
    emit(
      "update:modelValue",
      // date-fns parse gjør String() på input selv; eksplisitt her for typene.
      formatISO(parse(String(value), "dd.MM.yyyy HH:mm", defaultDate))
    );
  } catch (error) {
    // OBS (#82): $appInsights er ikke registrert noe sted (ingen boot-fil), så dette
    // kallet kaster TypeError. Komponenten er heller ikke i bruk.
    // @ts-expect-error -- $appInsights finnes ikke på ComponentCustomProperties (se OBS over)
    instance?.proxy?.$appInsights.trackException({
      exception: new Error(String(error)),
    });
  }
}
</script>
