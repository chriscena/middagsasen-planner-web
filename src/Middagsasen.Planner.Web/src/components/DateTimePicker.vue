<template>
  <q-input
    :model-value="formattedDate"
    @update:model-value="emitDate"
    :label="label"
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

<script>
import { format, formatISO, parse } from "date-fns";

const dateMask = "DD.MM.YYYY HH:mm";
const dateFormat = "dd.MM.yyyy HH:mm";
export default {
  // name: 'ComponentName',
  props: {
    modelValue: {
      type: String,
      default: () => null,
    },
    label: {
      type: String,
      default: () => null,
    },
    dense: {
      type: Boolean,
      default: () => false,
    },
    disable: {
      type: Boolean,
      default: () => false,
    },
    readonly: {
      type: Boolean,
      default: () => false,
    },
    status: {
      type: Number,
      default: () => null,
    },
    disabledDays: {
      type: Array,
      default: () => [],
    },
    defaultDate: {
      type: String,
      default: "",
    },
  },
  emits: ["update:modelValue"],
  computed: {
    formattedDefaultDate() {
      return this.defaultDate
        ? format(new Date(this.defaultDate), "yyyy/MM/dd")
        : format(new Date(), "yyyy/MM/dd");
    },
    dateFormat() {
      return dateFormat;
    },
    dateMask() {
      return dateMask;
    },
    formattedDate() {
      return this.modelValue
        ? format(new Date(this.modelValue), dateFormat)
        : null;
    },
  },
  watch: {
    modelValue: {
      handler(newValue, oldValue) {
        if (!newValue) this.selectedDate = null;
        if (newValue !== oldValue)
          this.selectedDate = format(new Date(newValue), dateFormat);
      },
      immediate: true,
    },
  },
  data() {
    return {
      selectedDate: null,
    };
  },
  methods: {
    emitDate(value) {
      if (!value) this.$emit("update:modelValue", null);
      try {
        const defaultDate = this.defaultDate
          ? new Date(this.defaultDate)
          : new Date();
        this.$emit(
          "update:modelValue",
          formatISO(parse(value, "dd.MM.yyyy HH:mm", defaultDate))
        );
      } catch (error) {
        this.$appInsights.trackException({ exception: new Error(error) });
      }
    },
    optionsFn(day) {
      return !this.disabledDays.includes(day);
    },
  },
};
</script>
