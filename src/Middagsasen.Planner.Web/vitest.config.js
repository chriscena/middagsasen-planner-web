import { defineConfig } from 'vitest/config';
import { resolve } from 'path';
import { fileURLToPath } from 'node:url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      src: resolve(__dirname, 'src'),
      stores: resolve(__dirname, 'src/stores'),
      boot: resolve(__dirname, 'src/boot'),
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.js', 'src/**/*.spec.js'],
  },
});
