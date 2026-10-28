# NRG Playground

The frontend is a React, TypeScript, and Relay client for the NRG Hot Chocolate
API. It signs in through the REST auth endpoints and uses the returned access
token for GraphQL requests.

## Run locally

```sh
npm install
npm run dev
```

Vite proxies `/auth` and `/graphql` to `https://localhost:7148` by default. Set
`NRG_API_URL` to use a different backend URL. For a deployed frontend, set
`VITE_API_URL` to the API's same-origin base path or configure the hosting
server to proxy `/auth` and `/graphql`.

## Session behavior

- Access tokens and their expiry are kept in module memory, never in
  `localStorage`.
- On startup, the app silently attempts `POST /auth/refresh` using the
  HttpOnly refresh-token cookie and waits before rendering.
- Sign-in and sign-out call `POST /auth/login` and `POST /auth/logout`.
  Sign-in sends `{ "username": "...", "password": "..." }`; successful login
  and refresh responses contain `{ "token": "...", "expiresAt": "..." }`.
- Relay refreshes a missing or near-expiry access token before GraphQL
  requests, and retries once after an authentication error.
- A new Relay environment is created for sign-in, sign-out, and expired
  sessions to prevent cached records from crossing identities.
- The UI reads role claims from the server-issued access token only to decide
  what to display. Role-protected GraphQL fields remain enforced by the API;
  a permission denial is shown in the feature instead of refreshing the token.
- `react-jwt` decodes the in-memory access token for the UI's role hint and
  signed-in greeting. Decoded claims are not trusted for API authorization.

The API must permit credentialed requests from a separately hosted frontend,
or be exposed through the same origin. The local Vite proxy provides the
same-origin development setup.

## Checks

```sh
npm run build
npm run lint
```
