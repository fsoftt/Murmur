# Directo — notes for AI assistants

- **No AI attribution** in commits, pull requests or code: no `Co-Authored-By` trailers,
  no "Generated with" footers, no session links. Commits are authored by the maintainer.
- Read `docs/architecture.md` and the ADRs in `docs/adr/` before changing behaviour.
  The wire protocol is specified in `docs/protocol/spec.md`; keep it in sync with the code.
- Never implement cryptographic primitives. Use the wrappers in `src/Directo.Security/Primitives`.
- Never log message plaintext, keys, tokens, invites or rendezvous topics.
- Build and test: `dotnet test Directo.slnx` (the MAUI app is not part of the solution filter
  used on Linux; see `README.md`).
