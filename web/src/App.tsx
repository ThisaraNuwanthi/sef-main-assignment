import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Link, Navigate, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { Layout } from './components/Layout'
import { ClassesPage } from './pages/ClassesPage'
import { ClassFormPage } from './pages/ClassFormPage'
import { CoachClassesPage } from './pages/CoachClassesPage'
import { DashboardPage } from './pages/DashboardPage'
import { EnrolmentsPage } from './pages/EnrolmentsPage'
import { LoginPage } from './pages/LoginPage'
import { WorkflowReviewPage } from './pages/WorkflowReviewPage'

/** All routes. Admin pages need the Admin role, coach pages the Coach role. */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<ProtectedRoute roles={['Admin']} />}>
        <Route element={<Layout />}>
          <Route path="/admin" element={<DashboardPage />} />
          <Route path="/admin/enrolments" element={<EnrolmentsPage />} />
          <Route path="/admin/workflows/:id" element={<WorkflowReviewPage />} />
          <Route path="/admin/classes" element={<ClassesPage />} />
          <Route path="/admin/classes/new" element={<ClassFormPage />} />
          <Route path="/admin/classes/:id/edit" element={<ClassFormPage />} />
        </Route>
      </Route>

      <Route element={<ProtectedRoute roles={['Coach']} />}>
        <Route element={<Layout />}>
          <Route path="/coach/classes" element={<CoachClassesPage />} />
        </Route>
      </Route>

      <Route path="/forbidden" element={<Message title="Access denied" text="Your account cannot open this page." />} />
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="*" element={<Message title="Page not found" text="That page does not exist." />} />
    </Routes>
  )
}

function Message({ title, text }: { title: string; text: string }) {
  return (
    <div className="login-page">
      <div className="card login-card">
        <h1>{title}</h1>
        <p>{text}</p>
        <Link to="/login">Go to sign in</Link>
      </div>
    </div>
  )
}

// Server data cache. Retry a failed request once; don't refetch just because the window got focus.
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 5_000 } },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  )
}
