# Roadmap

## v1.0.0 — Current Release

- [x] Apple Music (Windows Store) scrobbling via SMTC API
- [x] Album name resolution via Last.fm `track.getInfo`
- [x] Configurable scrobble threshold (% and max seconds)
- [x] Track normalization with regex rules (Deluxe Edition, Remastered, etc.)
- [x] Dark UI with sidebar navigation (Monitor, Account, Scrobbling, Normalization)
- [x] System tray integration
- [x] Scrobble history (local SQLite)
- [x] Optional edit-before-scrobble dialog
- [x] Windows startup option

## v1.1.0 — Polish

- [ ] Album art display in the monitor window
- [ ] Windows 11 toast notifications with album art
- [ ] Scrobble history browser (searchable list of past scrobbles)
- [ ] Retry queue for failed scrobbles (offline support)
- [ ] Tray icon changes to reflect current state (playing, idle, error)

## v1.2.0 — Multi-player support

- [ ] iTunes / Apple Music (legacy) support
- [ ] Spotify support via SMTC
- [ ] Any SMTC-compatible player (generic mode)
- [ ] Per-player normalization rules

## v1.3.0 — Power features

- [ ] Love/unlove track from tray menu
- [ ] Tag-based normalization (not just regex)
- [ ] Import/export normalization rules
- [ ] Manual scrobble entry

## v2.0.0 — Distribution

- [ ] MSIX packaging for Microsoft Store distribution
- [ ] Auto-updater
- [ ] Code signing

## Longer term / Ideas

- Scrobble from local media players (VLC, foobar2000) via plugin
- Last.fm friends' listening activity in the tray
- macOS companion app (using MusicKit / MediaPlayer APIs)

---

## Pending / Backlog

### About Page
- Standard "About this app" layout: app name, version, build date
- Donation button → spacechild.dev
- GitHub link, changelog link
- App description (one paragraph)

### Better Navigation & Menu Structure
- Account page is currently thin (login only) — options:
  - Merge Account into Settings as "Account & Settings"
  - Or keep separate but enrich with Last.fm profile info (avatar, total scrobbles, member since)
- Add About as a dedicated nav item
- Consider grouping: Monitor | History | Stats | Friends | Settings (with Account + About inside)

### Richer Account Page
- Show Last.fm profile picture, total scrobble count, member since (via `user.getInfo`)
- Auto-show login prompt on first run when no session is stored

### Custom Domain for Updates
- R2 currently at `pub-8a5464b225534730b481b262ffe4748b.r2.dev`
- Move to `updates.lastfm.spacechild.dev` (add CNAME in Cloudflare, update `UpdateChecker.ManifestUrl` + `build.ps1`)
