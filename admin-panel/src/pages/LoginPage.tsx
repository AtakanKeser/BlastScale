import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { API_BASE_URL, errorText, login } from '../api';
import { getSession, saveSession } from '../auth';

/** Username/password form. Only accounts with role ADMIN get a stored session; others see an inline error. */
export default function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const existing = getSession();
  if (existing && existing.role === 'ADMIN') {
    return <Navigate to="/" replace />;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const auth = await login(username.trim(), password);
      if (auth.role !== 'ADMIN') {
        setError(`FORBIDDEN: '${auth.username}' has role ${auth.role}; only ADMIN accounts may use this panel`);
        return;
      }
      saveSession(auth);
      const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';
      navigate(from, { replace: true });
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-box" onSubmit={submit}>
        <h1>BlastScale LiveOps</h1>
        <p className="muted">Sign in with an ADMIN account.</p>
        {error && <div className="error-box">{error}</div>}
        <div className="field">
          <label htmlFor="username">Username</label>
          <input
            id="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </div>
        <div className="field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </div>
        <button type="submit" className="btn-primary" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
        <p className="muted" style={{ marginBottom: 0 }}>
          API: {API_BASE_URL}
        </p>
      </form>
    </div>
  );
}
