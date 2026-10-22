import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  viteConfig,
  defineConfig({
    plugins: [react()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      restoreMocks: true,
      include: ['src/**/*.{test,spec}.{ts,tsx}'],
      /*
       * The per-test timeout is raised from vitest's 5 s default.
       *
       * `MenuRecipeFormPage.test.tsx` types roughly sixty characters with
       * `userEvent.type`, which is slow by design (every keystroke is awaited).
       * It sat ~1.7 s under the old ceiling and failed intermittently when a
       * heavier suite ran beside it on the default worker pool.
       *
       * Raising the ceiling removes the flakiness without touching a single
       * test file and without changing the suite's wall-clock time - a slow
       * test is still slow, it is simply no longer racing a 5 s budget.
       */
      testTimeout: 15000,
    },
  }),
)
