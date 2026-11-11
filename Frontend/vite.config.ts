import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      // ws: true – przepuszcza też połączenie WebSocket do huba list (/api/hubs/todo-lists).
      '/api': { target: 'http://localhost:5196', ws: true },
    },
  },
})