/**
 * مسیریابی Vue — قبل از هر صفحه بررسی می‌کند کاربر وارد شده یا نقشش کافی است.
 */
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AppLayout from '@/layouts/AppLayout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guest: true }, // فقط برای کسانی که لاگین نکرده‌اند
    },
    {
      path: '/',
      component: AppLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
        { path: 'rugs', name: 'rugs', component: () => import('@/views/RugsView.vue') },
        {
          path: 'rugs/new',
          name: 'rug-create',
          component: () => import('@/views/RugCreateView.vue'),
          meta: { hideBottomNav: true },
        },
        {
          path: 'rugs/:id',
          name: 'rug-detail',
          component: () => import('@/views/RugDetailView.vue'),
          meta: { hideBottomNav: true },
        },
        {
          path: 'labels/designer',
          name: 'label-designer',
          component: () => import('@/views/LabelDesignerView.vue'),
        },
        { path: 'workflows', name: 'workflows', component: () => import('@/views/WorkflowsView.vue') },
        {
          path: 'admin/tenants',
          name: 'admin-tenants',
          component: () => import('@/views/AdminTenantsView.vue'),
          meta: { systemAdmin: true },
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.user && auth.token) await auth.hydrate()

  if (to.meta.requiresAuth && !auth.isAuthenticated) return { name: 'login' }
  if (to.meta.guest && auth.isAuthenticated) return { name: 'dashboard' }
  if (to.meta.systemAdmin && !auth.isSystemAdmin) return { name: 'dashboard' }
})

export default router
