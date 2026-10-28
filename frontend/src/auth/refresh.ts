import { authUrl, readAccessTokenResponse } from "./api";
import { authEvents } from "./events";
import { tokenStore } from "./tokenStore";

let refreshInFlight: Promise<string | null> | null = null;

export function refreshAccessToken(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    try {
      const response = await fetch(authUrl("refresh"), {
        method: "POST",
        credentials: "include",
      });
      if (response.status === 401) {
        tokenStore.clear();
        return null;
      }
      if (!response.ok) {
        throw new Error(`Session refresh failed (HTTP ${response.status}).`);
      }

      const data = await readAccessTokenResponse(response);
      tokenStore.set(data.token, data.expiresAt);
      authEvents.dispatchEvent(new Event("token-updated"));
      return data.token;
    } catch (error) {
      tokenStore.clear();
      if (error instanceof Error) throw error;
      throw new Error("Session refresh failed.", { cause: error });
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}
