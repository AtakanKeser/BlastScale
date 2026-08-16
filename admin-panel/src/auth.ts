// Session persistence: the login response is kept in localStorage and attached as a bearer token
// to every API call (see api.ts).
import type { AuthResponse } from './types';

/** What is persisted after a successful admin login. */
export interface Session {
  token: string;
  expiresAt: string;
  playerId: number;
  username: string;
  role: string;
}

const STORAGE_KEY = 'blastscale.admin.session';

/** Returns the stored session, or null when there is none, it is unreadable, or the token has expired. */
export function getSession(): Session | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    if (!session.token || !session.expiresAt) return null;
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      clearSession();
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

/** Persists the login response for later requests. */
export function saveSession(auth: AuthResponse): void {
  const session: Session = {
    token: auth.token,
    expiresAt: auth.expiresAt,
    playerId: auth.playerId,
    username: auth.username,
    role: auth.role,
  };
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

/** Forgets the session (logout, or a 401 from the API). */
export function clearSession(): void {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // storage unavailable: nothing to clear
  }
}

/** True when a non-expired session with role ADMIN is stored. */
export function hasAdminSession(): boolean {
  const session = getSession();
  return session !== null && session.role === 'ADMIN';
}
