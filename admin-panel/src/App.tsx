import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import Layout from './components/Layout';
import { ToastProvider } from './components/Toast';
import { hasAdminSession } from './auth';
import ConfigPage from './pages/ConfigPage';
import DashboardPage from './pages/DashboardPage';
import EventsPage from './pages/EventsPage';
import ExperimentsPage from './pages/ExperimentsPage';
import LeaderboardPage from './pages/LeaderboardPage';
import LevelsPage from './pages/LevelsPage';
import LoginPage from './pages/LoginPage';
import PlayerDetailPage from './pages/PlayerDetailPage';
import PlayersPage from './pages/PlayersPage';
import SystemPage from './pages/SystemPage';

/** Route guard: renders the app shell only for a valid ADMIN session, otherwise sends the user to /login. */
function RequireAdmin() {
  const location = useLocation();
  if (!hasAdminSession()) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }
  return <Layout />;
}

/** Route table. Every page except /login is nested under the guarded Layout (sidebar + top bar). */
export default function App() {
  return (
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<RequireAdmin />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/players" element={<PlayersPage />} />
            <Route path="/players/:id" element={<PlayerDetailPage />} />
            <Route path="/events" element={<EventsPage />} />
            <Route path="/experiments" element={<ExperimentsPage />} />
            <Route path="/config" element={<ConfigPage />} />
            <Route path="/leaderboard" element={<LeaderboardPage />} />
            <Route path="/levels" element={<LevelsPage />} />
            <Route path="/system" element={<SystemPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ToastProvider>
  );
}
