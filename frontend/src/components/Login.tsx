import { useState, type FormEvent } from "react";
import { useAuth } from "../auth/context";

function Login() {
  const { isLoggedIn, login, logout, isLoading, bootstrapError } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    try {
      await login(username, password);
      setPassword("");
    } catch (loginError) {
      setError(loginError instanceof Error ? loginError.message : "Sign-in failed.");
    }
  };

  const handleLogout = async () => {
    setError(null);
    try {
      await logout();
    } catch (logoutError) {
      setError(logoutError instanceof Error ? logoutError.message : "Sign-out failed.");
    }
  };

  if (isLoggedIn) {
    return (
      <div className="account-state">
        <div className="signed-in">
          <span className="signed-in-icon" aria-hidden="true">✓</span>
          <div>
            <strong>Signed in</strong>
            <p>Your protected data is ready.</p>
          </div>
        </div>
        <button
          className="button button-secondary"
          onClick={handleLogout}
          disabled={isLoading}
        >
          {isLoading ? "Signing out…" : "Sign out"}
        </button>
        {error && <p className="form-error" role="alert">{error}</p>}
      </div>
    );
  }

  return (
    <form className="login-form" onSubmit={handleSubmit}>
      <label htmlFor="username">Username</label>
      <input
        id="username"
        type="text"
        autoComplete="username"
        placeholder="Your username"
        value={username}
        onChange={(event) => setUsername(event.target.value)}
        required
      />
      <label htmlFor="password">Password</label>
      <input
        id="password"
        type="password"
        autoComplete="current-password"
        placeholder="Your password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        required
      />
      {bootstrapError && <p className="form-error" role="alert">{bootstrapError}</p>}
      {error && <p className="form-error" role="alert">{error}</p>}
      <button className="button button-primary" type="submit" disabled={isLoading}>
        {isLoading ? "Signing in…" : "Sign in"}
      </button>
    </form>
  );
}

export default Login;
