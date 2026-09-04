import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';
import { errorText } from '../api';

type ToastKind = 'success' | 'error' | 'info';

interface ToastItem {
  id: number;
  kind: ToastKind;
  text: string;
}

/** What pages get from useToast(): `error` accepts anything thrown and renders API errors as "CODE: message". */
export interface ToastApi {
  success: (text: string) => void;
  error: (error: unknown) => void;
  info: (text: string) => void;
}

const ToastContext = createContext<ToastApi | null>(null);
const TOAST_MS = 6000;

/** Holds the toast queue and renders it fixed in the bottom-right corner; a toast disappears after 6s or on click. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const push = useCallback((kind: ToastKind, text: string) => {
    const id = nextId.current++;
    setToasts((current) => [...current, { id, kind, text }]);
    window.setTimeout(() => setToasts((current) => current.filter((t) => t.id !== id)), TOAST_MS);
  }, []);

  const dismiss = (id: number) => setToasts((current) => current.filter((t) => t.id !== id));

  const api = useMemo<ToastApi>(
    () => ({
      success: (text) => push('success', text),
      error: (error) => push('error', errorText(error)),
      info: (text) => push('info', text),
    }),
    [push],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toasts">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.kind}`} onClick={() => dismiss(toast.id)}>
            {toast.text}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

/** Access to the toast queue from any page. */
export function useToast(): ToastApi {
  const context = useContext(ToastContext);
  if (!context) throw new Error('useToast must be used inside <ToastProvider>');
  return context;
}
