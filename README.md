# C# Auth Playground

A playground for exploring authentication between a C# API and a React client:
how JWT access tokens and rotating refresh tokens work together, and how the
API can restrict endpoints and GraphQL fields based on a user's role.

## Technologies

- **Backend:** .NET 10, ASP.NET Core minimal APIs, Hot Chocolate GraphQL, and
  Entity Framework Core with SQLite.
- **Frontend:** React, TypeScript, Relay, and Vite.

The backend and frontend are in `backend` and `frontend`, respectively.

## Authentication overview

- The API exposes REST endpoints for login, refresh, and logout. On successful
  login it issues a signed JWT access token and a refresh token.
- The frontend keeps the access token in memory and sends it with GraphQL
  requests. It attempts to restore sessions on startup and refreshes the access
  token as it nears expiry.
- The refresh token is sent in an `HttpOnly`, `Secure`, `SameSite=Strict`
  cookie, scoped to the auth endpoints. The API stores only its hash.
- Each refresh rotates the refresh token. Reuse of a revoked token revokes its
  active token family; logout also revokes the family.
- The API validates JWTs and enforces authorization on protected minimal API
  endpoints and GraphQL fields, including role-based access. Frontend role
  checks only control what the UI displays; the API remains the authority.

This is an experimental learning project, not a production-ready
authentication template. In particular, review and replace demo security
choices before using these patterns in a real application.
