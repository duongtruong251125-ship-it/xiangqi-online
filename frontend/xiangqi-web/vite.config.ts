import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: { environment: 'jsdom', setupFiles: './src/test/setup.ts', css: true },
  server: { proxy: { '/api/v1': { target: process.env.XIANGQI_API_PROXY || 'https://localhost:7080', secure: false } } },
})
