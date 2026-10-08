# Murmur — notes for AI assistants

- **No AI attribution** in commits, pull requests or code: no `Co-Authored-By` trailers,
  no "Generated with" footers, no session links. Commits are authored by the maintainer.
- Read `docs/architecture.md` and the ADRs in `docs/adr/` before changing behaviour.
  The wire protocol is specified in `docs/protocol/spec.md`; keep it in sync with the code.
- Never implement cryptographic primitives. Use the wrappers in `src/Murmur.Security/Primitives`.
- Never log message plaintext, keys, tokens, invites or rendezvous topics.
- Build and test: `dotnet test Murmur.slnx` (the MAUI app is not part of the solution filter
  used on Linux; see `README.md`).
- The project site lives in `website/` (VitePress, deployed by `.github/workflows/pages.yml`).
  When behaviour, architecture or decisions change, update `README.md`, `README.es.md`, `docs/` and `website/` together.
  The site is bilingual: English pages at the root of `website/`, Spanish under `website/es/`; keep both in sync.
  Check it locally with `cd website && npm ci && npm run build`.
