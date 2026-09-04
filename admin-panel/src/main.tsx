import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './styles.css';

// Application entry point: mounts the router-driven app into #root.
ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
