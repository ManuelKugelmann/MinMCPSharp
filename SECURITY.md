# Security Policy

## Supported Versions

The latest release is supported with security fixes.

## Known Security Considerations

MinMCPSharp is a lightweight library intended for local/trusted environments. The following are known areas to be aware of when deploying:

### Transport Layer

- **No built-in authentication/authorization** — any client that can reach the server can invoke tools. Add an authentication layer if exposing beyond localhost.
- **CORS is set to wildcard (`*`)** — restrict `AllowedOrigins` when serving browser clients.
- **Origin validation uses substring matching** — origins like `localhost.attacker.com` may bypass the default check. Use explicit `AllowedOrigins` instead.
- **No request size limits** — large payloads can exhaust memory. Consider a reverse proxy with size limits in production.

### Error Handling

- **Stack traces are included in error responses** — tool invocation errors return full stack traces to the client, which may leak internal details.
- **Exception messages are returned in HTTP 500 responses** — may disclose file paths or internal state.

### Deserialization & Reflection

- **`JToken.ToObject()` and `Convert.ChangeType()` are used without type whitelists** — only expose tools with safe parameter types.
- **`Activator.CreateInstance()` is called on tool declaring types per invocation** — tool classes should have safe parameterless constructors.

### Client (McpClient)

- **Stdio client spawns child processes** — validate `command` and `args` if they come from untrusted input.
- **Permission gate is client-side only** — it does not provide server-side enforcement.

## Reporting a Vulnerability

Please open a security advisory at https://github.com/ManuelKugelmann/MinMCPSharp/security/advisories.
