const configuredApiUrl = import.meta.env.VITE_API_URL ?? "";
const apiUrl = configuredApiUrl.replace(/\/$/, "");

export function authUrl(path: "login" | "logout" | "refresh"): string {
  return `${apiUrl}/auth/${path}`;
}

export function graphqlUrl(): string {
  return `${apiUrl}/graphql`;
}

export interface AccessTokenResponse {
  token: string;
  expiresAt: string;
}

export async function readAccessTokenResponse(
  response: Response,
): Promise<AccessTokenResponse> {
  const result: unknown = await response.json();
  if (
    typeof result !== "object" ||
    result === null ||
    !("token" in result) ||
    typeof result.token !== "string" ||
    !("expiresAt" in result) ||
    typeof result.expiresAt !== "string"
  ) {
    throw new Error("The authentication server returned an invalid token response.");
  }

  return { token: result.token, expiresAt: result.expiresAt };
}
