import {
  Environment,
  Network,
  RecordSource,
  Store,
  type FetchFunction,
} from "relay-runtime";
import { graphqlUrl } from "../auth/api";
import { AuthorizationError } from "../auth/errors";
import { authEvents } from "../auth/events";
import { refreshAccessToken } from "../auth/refresh";
import { tokenStore } from "../auth/tokenStore";

const TOKEN_EXPIRY_SKEW_MS = 30_000;

async function ensureAccessToken(): Promise<string | null> {
  const token = tokenStore.get();
  const expiry = tokenStore.getExpiry();
  if (!token || (expiry !== null && expiry - Date.now() <= TOKEN_EXPIRY_SKEW_MS)) {
    return refreshAccessToken();
  }

  return token;
}

function isAuthenticationError(
  response: { errors?: ReadonlyArray<{ extensions?: { code?: unknown } }> },
  status: number,
): boolean {
  return (
    status === 401 ||
    response.errors?.some(({ extensions }) =>
      extensions?.code === "AUTH_NOT_AUTHENTICATED",
    ) === true
  );
}

function hasUnauthorizedError(
  response: { errors?: ReadonlyArray<{ extensions?: { code?: unknown } }> },
  status: number,
): boolean {
  return (
    status === 401 ||
    response.errors?.some(
      ({ extensions }) => extensions?.code === "AUTH_NOT_AUTHENTICATED",
    ) === true
  );
}

function hasAuthorizationError(
  response: { errors?: ReadonlyArray<{ extensions?: { code?: unknown } }> },
  status: number,
): boolean {
  return (
    status === 403 ||
    response.errors?.some(
      ({ extensions }) => extensions?.code === "AUTH_NOT_AUTHORIZED",
    ) === true
  );
}

async function sendRequest(
  request: Parameters<FetchFunction>[0],
  variables: Parameters<FetchFunction>[1],
  token: string | null,
): Promise<{ response: Response; json: Awaited<ReturnType<Response["json"]>> }> {
  const response = await fetch(graphqlUrl(), {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      "GraphQL-Preflight": "1",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ query: request.text, variables }),
  });

  const json: unknown =
    response.status === 401 || response.status === 403
      ? {}
      : await response.json();
  return { response, json };
}

const fetchQuery: FetchFunction = async (request, variables) => {
  let token = await ensureAccessToken();
  let result = await sendRequest(request, variables, token);
  const initialResponse = result.json as {
    errors?: ReadonlyArray<{ extensions?: { code?: unknown } }>;
  };

  if (isAuthenticationError(initialResponse, result.response.status)) {
    token = await refreshAccessToken();
    if (!token) {
      tokenStore.clear();
      authEvents.dispatchEvent(new Event("session-expired"));
      throw new Error("Your session has expired. Please sign in again.");
    }

    result = await sendRequest(request, variables, token);
    const retryResponse = result.json as {
      errors?: ReadonlyArray<{ extensions?: { code?: unknown } }>;
    };
    if (hasUnauthorizedError(retryResponse, result.response.status)) {
      tokenStore.clear();
      authEvents.dispatchEvent(new Event("session-expired"));
      throw new Error("Your session has expired. Please sign in again.");
    }
    if (hasAuthorizationError(retryResponse, result.response.status)) {
      throw new AuthorizationError();
    }
  } else if (hasAuthorizationError(initialResponse, result.response.status)) {
    throw new AuthorizationError();
  }

  return result.json;
};

export function createRelayEnvironment(): Environment {
  return new Environment({
    network: Network.create(fetchQuery),
    store: new Store(new RecordSource()),
  });
}
