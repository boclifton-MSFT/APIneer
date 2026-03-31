// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  modules: ['@nuxt/ui', '@pinia/nuxt'],

  css: ['~/assets/css/main.css'],

  icon: {
    serverBundle: 'local'
  },

  routeRules: {
    '/api/**': { proxy: 'http://localhost:5000/api/**' }
  }
})
