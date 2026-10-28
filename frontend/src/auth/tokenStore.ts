import {
  getRolesFromAccessToken,
  getUserNameFromAccessToken,
} from "./tokenClaims";

let accessToken: string | null = null;
let expiresAt: number | null = null;
let roles: readonly string[] = [];
let userName: string | null = null;

export const tokenStore = {
  get: () => accessToken,
  getExpiry: () => expiresAt,
  getRoles: () => roles,
  getUserName: () => userName,
  set: (token: string, expiry: string) => {
    const parsedExpiry = Date.parse(expiry);
    if (!Number.isFinite(parsedExpiry)) {
      throw new Error("The authentication server returned an invalid token expiry.");
    }

    accessToken = token;
    expiresAt = parsedExpiry;
    roles = getRolesFromAccessToken(token);
    userName = getUserNameFromAccessToken(token);
  },
  clear: () => {
    accessToken = null;
    expiresAt = null;
    roles = [];
    userName = null;
  },
};
