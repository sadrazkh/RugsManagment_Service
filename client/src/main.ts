/**
 * نقطهٔ ورود فرانت — استایل، Pinia، Router، و ثبت Service Worker برای PWA
 */
import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { registerSW } from 'virtual:pwa-register'

import App from './App.vue'
import router from './router'

// به‌روزرسانی خودکار PWA وقتی نسخهٔ جدید build شود
registerSW({ immediate: true })

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
