import { ErrorBoundary, type FallbackProps } from "react-error-boundary";
import "./App.css";
import "./components/Login.css";
import { AuthProvider } from "./auth/AuthProvider";
import Login from "./components/Login";
import DataDisplayAuthorized from "./components/DataDisplayAuthorized";
import DataDisplayPublic from "./components/DataDisplayPublic";
import DataDisplayRole from "./components/DataDisplayRole";
import { useAuth } from "./auth/context";
import { ThemeButton } from "./components/theme/ThemeButton";
import { ThemeProvider } from "./components/theme/ThemeProvider";

function ErrorFallback({ error, resetErrorBoundary }: FallbackProps) {
  return (
    <main className="app-shell">
      <section className="error-card" role="alert">
        <p className="eyebrow">Something went wrong</p>
        <h1>The playground hit a snag.</h1>
        <p>{error instanceof Error ? error.message : String(error)}</p>
        <button
          className="button button-secondary"
          onClick={resetErrorBoundary}
        >
          Try again
        </button>
      </section>
    </main>
  );
}

function AppContent() {
  const { isLoggedIn, userName } = useAuth();

  return (
    <main className="app-shell">
      <header className="site-header">
        <a className="brand" href="/" aria-label="NRG Playground home">
          <span className="brand-mark" aria-hidden="true">
            N
          </span>
          <span>
            NRG <span className="brand-light">Playground</span>
          </span>
        </a>
        <span className="connection-status">
          <span className="status-dot" aria-hidden="true" />
          API playground
        </span>
        <ThemeProvider>
          <ThemeButton />
        </ThemeProvider>
      </header>

      <section className="welcome">
        <p className="eyebrow">Relay + Hot Chocolate</p>
        <h1>
          Your workspace, <span>securely connected.</span>
        </h1>
        <p className="welcome-copy">
          Sign in to explore protected GraphQL data. Your session is restored
          securely when you return.
        </p>
      </section>

      {isLoggedIn && (
        <section className="hello-banner" aria-live="polite">
          <span className="hello-icon" aria-hidden="true">
            ✓
          </span>
          <p>
            Hello, <strong>{userName ?? "there"}</strong>! You’re signed in.
          </p>
        </section>
      )}

      <section className="workspace" aria-label="Authentication and data">
        <aside className="auth-panel">
          <p className="eyebrow">Your account</p>
          <h2>Session</h2>
          <Login />
        </aside>
        <div className="data-panel">
          <div className="section-heading">
            <div>
              <p className="eyebrow">General access</p>
              <h2>Public data</h2>
            </div>
            <span className="data-tag">AllowAnonymous</span>
          </div>
          <DataDisplayPublic />

          <div className="data-divider" />

          <div className="section-heading">
            <div>
              <p className="eyebrow">With data from JWT</p>
              <h2>Protected data</h2>
            </div>
            <span className="data-tag">Authorized</span>
          </div>
          <DataDisplayAuthorized />

          <div className="data-divider" />

          <div className="section-heading">
            <div>
              <p className="eyebrow">With special auth</p>
              <h2>Protected data</h2>
            </div>
            <span className="data-tag">Role: Admin</span>
          </div>
          <DataDisplayRole />
        </div>
      </section>

      <footer className="site-footer">
        <span>NRG Playground</span>
        <span>Access tokens stay in memory. Refresh tokens stay HttpOnly.</span>
      </footer>
    </main>
  );
}

function App() {
  return (
    <ErrorBoundary FallbackComponent={ErrorFallback}>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </ErrorBoundary>
  );
}

export default App;
