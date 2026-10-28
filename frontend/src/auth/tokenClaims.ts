import { decodeToken } from "react-jwt";

type AccessTokenClaims = Record<string, unknown>;

const roleClaimTypes = [
  "role",
  "roles",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
] as const;

const displayNameClaimTypes = [
  "name",
  "unique_name",
  "email",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
] as const;

export function getAccessTokenClaims(token: string): AccessTokenClaims | null {
  return decodeToken<AccessTokenClaims>(token);
}

export function getRolesFromAccessToken(token: string): readonly string[] {
  const claims = getAccessTokenClaims(token);
  if (!claims) return [];

  const roles = roleClaimTypes.flatMap((claimType) => {
    const value = claims[claimType];
    if (typeof value === "string") return [value];
    if (Array.isArray(value)) {
      return value.filter((role): role is string => typeof role === "string");
    }
    return [];
  });

  return [...new Set(roles)];
}

export function getUserNameFromAccessToken(token: string): string | null {
  const claims = getAccessTokenClaims(token);
  if (!claims) return null;

  for (const claimType of displayNameClaimTypes) {
    const value = claims[claimType];
    if (typeof value === "string" && value.trim()) return value;
  }

  return null;
}
