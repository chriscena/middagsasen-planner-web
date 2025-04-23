<template>
  <q-virtual-scroll
    :items="virtualScrollLength"
    :items-fn="getItems"
    :virtual-scroll-item-size="50"
    separator
  >
    <template #default="{ item, index }">
      <q-item :key="index">
        <q-item-section>{{ item?.label || "Loading..." }}</q-item-section>
      </q-item>
    </template>
  </q-virtual-scroll>
</template>

<script setup>
import { ref } from "vue";

// Placeholder length (can be estimated or fetched separately)
const virtualScrollLength = 1000;

// Local cache of fetched items
const itemCache = ref({});

// Mock API fetcher (replace with real API)
async function fetchRemoteItems(from, size) {
  // Simulate network delay
  await new Promise((resolve) => setTimeout(resolve, 300));

  // Simulate API response
  return Array.from({ length: size }, (_, i) => ({
    label: `Remote Item ${from + i + 1}`,
  }));
}

// QVirtualScroll item loader
async function getItems(from, size) {
  const items = [];

  for (let i = 0; i < size; i++) {
    const index = from + i;

    if (itemCache.value[index]) {
      items.push(itemCache.value[index]);
    } else {
      // Placeholder for now
      items.push(null);
    }
  }

  // Fetch missing items in background
  const missingIndices = items
    .map((item, i) => (item === null ? from + i : null))
    .filter((i) => i !== null);

  if (missingIndices.length > 0) {
    const minIndex = Math.min(...missingIndices);
    const maxIndex = Math.max(...missingIndices);
    const fetchSize = maxIndex - minIndex + 1;

    const newItems = await fetchRemoteItems(minIndex, fetchSize);

    newItems.forEach((item, i) => {
      itemCache.value[minIndex + i] = item;
    });
  }

  // Return final set, after fetching
  return Object.freeze(
    Array.from({ length: size }, (_, i) => itemCache.value[from + i] || null)
  );
}
</script>
