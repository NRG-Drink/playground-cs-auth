import {
  useCallback,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { RelayEnvironmentProvider } from "react-relay";
import { authUrl, readAccessTokenResponse } from "./api";
import { AuthContext } from "./context";
import { authEvents } from "./events";
import { refreshAccessToken } from "./refresh";
import { tokenStore } from "./tokenStore";
import { createRelayEnvironment } from "../relay/environment";

const SCHEDULED_REFRESH_BUFFER_MS = 60_000;

function FullPageSpinner() {
  return (
    <main className="app-loading" role="status" aria-live="polite">
      <span className="loading-indicator" aria-hidden="true" />
      <p>Restoring your session…</p>
    </main>
  );
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [environment, setEnvironment] = useState(createRelayEnvironment);
  const [sessionKey, setSessionKey] = useState(0);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [roles, setRoles] = useState<readonly string[]>([]);
  const [userName, setUserName] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);

  const resetRelaySession = useCallback(() => {
    setEnvironment(createRelayEnvironment());
    setSessionKey((key) => key + 1);
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const token = await refreshAccessToken();
        if (active) {
          setIsLoggedIn(token !== null);
          setRoles(token ? tokenStore.getRoles() : []);
          setUserName(token ? tokenStore.getUserName() : null);
        }
      } catch (error: unknown) {
        if (active) {
          setBootstrapError(
            error instanceof Error
              ? `Could not restore your session: ${error.message}`
              : "Could not restore your session.",
          );
        }
      } finally {
        if (active) setIsBootstrapping(false);
      }
    })();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    let refreshTimeout: ReturnType<typeof setTimeout> | null = null;

    const clearRefreshTimeout = () => {
      if (refreshTimeout !== null) {
        clearTimeout(refreshTimeout);
        refreshTimeout = null;
      }
    };

    const scheduleRefresh = () => {
      clearRefreshTimeout();

      if (document.visibilityState !== "visible") return;

      const expiry = tokenStore.getExpiry();
      if (expiry === null || tokenStore.get() === null) return;

      const timeUntilExpiry = expiry - Date.now();
      if (timeUntilExpiry <= 0) return;

      const refreshBuffer = Math.min(
        SCHEDULED_REFRESH_BUFFER_MS,
        timeUntilExpiry / 10,
      );
      const delay = timeUntilExpiry - refreshBuffer;
      refreshTimeout = setTimeout(() => {
        refreshTimeout = null;
        if (document.visibilityState === "visible") refreshInBackground();
      }, delay);
    };

    const refreshInBackground = () => {
      clearRefreshTimeout();
      void refreshAccessToken()
        .then((token) => {
          if (!active) return;
          if (token === null) {
            setBootstrapError("Your session has expired. Please sign in again.");
            authEvents.dispatchEvent(new Event("session-expired"));
          }
        })
        .catch((error: unknown) => {
          if (!active) return;
          setBootstrapError(
            error instanceof Error
              ? `Could not refresh your session: ${error.message}`
              : "Could not refresh your session.",
          );
          authEvents.dispatchEvent(new Event("session-expired"));
        });
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState !== "visible") {
        clearRefreshTimeout();
        return;
      }

      const expiry = tokenStore.getExpiry();
      if (
        expiry !== null &&
        tokenStore.get() !== null &&
        expiry - Date.now() <= SCHEDULED_REFRESH_BUFFER_MS
      ) {
        refreshInBackground();
      } else {
        scheduleRefresh();
      }
    };

    const handleSessionExpired = () => {
      clearRefreshTimeout();
      tokenStore.clear();
      setIsLoggedIn(false);
      setRoles([]);
      setUserName(null);
      resetRelaySession();
    };

    const handleTokenUpdated = () => {
      setRoles(tokenStore.getRoles());
      setUserName(tokenStore.getUserName());
      scheduleRefresh();
    };

    authEvents.addEventListener("session-expired", handleSessionExpired);
    authEvents.addEventListener("token-updated", handleTokenUpdated);
    document.addEventListener("visibilitychange", handleVisibilityChange);
    handleVisibilityChange();
    return () => {
      active = false;
      clearRefreshTimeout();
      authEvents.removeEventListener("session-expired", handleSessionExpired);
      authEvents.removeEventListener("token-updated", handleTokenUpdated);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [resetRelaySession]);

  const login = useCallback(async (username: string, password: string) => {
    setIsLoading(true);
    setBootstrapError(null);
    try {
      const response = await fetch(authUrl("login"), {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        throw new Error(
          response.status === 401
            ? "Invalid username or password."
            : `Sign-in failed (HTTP ${response.status}).`,
        );
      }

      const data = await readAccessTokenResponse(response);
      tokenStore.set(data.token, data.expiresAt);
      setIsLoggedIn(true);
      setRoles(tokenStore.getRoles());
      setUserName(tokenStore.getUserName());
      authEvents.dispatchEvent(new Event("token-updated"));
      resetRelaySession();
    } finally {
      setIsLoading(false);
    }
  }, [resetRelaySession]);

  const logout = useCallback(async () => {
    let logoutError: Error | null = null;
    try {
      const response = await fetch(authUrl("logout"), {
        method: "POST",
        credentials: "include",
      });
      if (!response.ok) {
        logoutError = new Error(`Sign-out failed (HTTP ${response.status}).`);
      }
    } catch (error) {
      logoutError = error instanceof Error ? error : new Error("Sign-out failed.");
    } finally {
      authEvents.dispatchEvent(new Event("session-expired"));
    }

    if (logoutError) throw logoutError;
  }, []);

  if (isBootstrapping) {
    return <FullPageSpinner />;
  }

  return (
    <AuthContext.Provider
      value={{
        isLoggedIn,
        login,
        logout,
        isLoading,
        bootstrapError,
        hasRole: (role) => roles.includes(role),
        userName,
      }}
    >
      <RelayEnvironmentProvider key={sessionKey} environment={environment}>
        {children}
      </RelayEnvironmentProvider>
    </AuthContext.Provider>
  );
}
