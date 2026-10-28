// Fetches the SDL from a running backend instance (HotChocolate exposes it via `?sdl`)
// so relay-compiler has an up-to-date schema.graphql without needing to stop the dev server.
const endpoint = process.env.VITE_GRAPHQL_ENDPOINT ?? 'http://localhost:5180/graphql/'

const response = await fetch(`${endpoint}?sdl`)
if (!response.ok) {
  throw new Error(
    `Failed to fetch schema from ${endpoint}: ${response.status} ${response.statusText}`,
  )
}

const sdl = await response.text()
await import('node:fs/promises').then((fs) => fs.writeFile('schema.graphql', sdl, 'utf8'))

console.log(`Wrote schema.graphql from ${endpoint}`)
