import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { API_BASE_URL } from '../api';
import { clearSession, getSession } from '../auth';
import { formatDateTime } from '../format';

/** Sidebar entries, in display order. */
const NAV: { to: string; label: string }[] = [
  { to: '/', label: 'Dashboard' },
  { to: '/players', label: 'Players' },
  { to: '/events', label: 'Live events' },
  { to: '/experiments', label: 'Experiments' },
  { to: '/config', label: 'Remote config' },
  { to: '/leaderboard', label: 'Leaderboard' },
  { to: '/levels', label: 'Levels' },
  { to: '/system', label: 'System' },
];

/** Application shell: left sidebar with the pages, top bar with the signed-in admin, page content below. */
export default function Layout() {
  const navigate = useNavigate();
  const session = getSession();

  function logout() {
    clearSession();
    navigate('/login', { replace: true });
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="brand">
          BlastScale
          <span>LiveOps console</span>
        </div>
        <nav>
          {NAV.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-footer">API: {API_BASE_URL}</div>
      </aside>
      <div className="main">
        <header className="topbar">
          <span>
            Signed in as <strong>{session?.username ?? '?'}</strong>{' '}
            <span className="badge badge-blue">{session?.role ?? 'UNKNOWN'}</span>
            <span className="muted"> · token expires {formatDateTime(session?.expiresAt)}</span>
          </span>
          <button type="button" onClick={logout}>
            Log out
          </button>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
