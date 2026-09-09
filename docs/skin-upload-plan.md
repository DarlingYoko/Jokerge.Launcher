# In-Launcher Skin & Cloak Upload — Implementation Plan

## Goal
Let a user change their Minecraft skin (and cloak/cape) directly inside the Jokerge
Launcher: click a button, pick a file, upload it to the backend. The uploaded texture
is then used by the game in both singleplayer/local worlds and on servers.

**Decisions locked in:**
- Code location: **launcher-only** (no changes to the `Gml.Client` submodule).
- Scope for v1: **skins + cloak/cape**.

---

## Background — how skins work today

### Reading (already implemented)
- `ProfilePageViewModel.LoadData()` calls `_manager.GetTexturesByName(name)` → returns a
  skin URL (`FullSkinUrl`).
- `ProfileUserControl` renders it via the `AsyncSkinRenderLoader` attached property, which
  downloads the PNG and passes it through `SkinViewer` (ImageSharp) to draw the body.
- Auth also returns `TextureUrl` (`ApiProcedures.cs` ~line 397/455).
- Skin URL resolves to either an external URL or
  `{Host}/api/v1/integrations/texture/skins/{TextureSkinGuid}`.

### Uploading (missing in the launcher, but the backend supports it)
Neither `GmlClientManager` nor `ApiProcedures` (in the `Gml.Client` submodule) has an
upload method. The backend (`Gml.Web.Api`, `TextureIntegrationHandler`) already exposes:

| Method | Route | Body | Auth |
|---|---|---|---|
| `POST` | `/api/v1/integrations/texture/skins/load`  | multipart: `Login` (text) + `Texture` (file) | `Authorization: Bearer <user AccessToken>` |
| `POST` | `/api/v1/integrations/texture/cloaks/load` | same shape | same |

Related read routes (for reference):
- `GET /api/v1/integrations/texture/skins/{textureGuid}` — serves the skin PNG.
- `GET /api/v1/integrations/texture/capes/{textureGuid}` — serves the cape PNG.
- `GET /api/v1/integrations/texture/head/{userUuid}` — serves a head render.

### Why no game-side changes are needed (local + server worlds)
The launcher already injects **authlib-injector** into the game
(`{authEndpoint}` → `/api/v1/integrations/authlib/minecraft`, `ApiProcedures.cs` ~line 232).
authlib makes the vanilla client fetch skin/cape textures from the backend's texture
service for the player's own profile — so once a skin is uploaded to the backend, the game
shows it in singleplayer and on any server using that same auth. **The whole feature
reduces to: file picker → POST to backend → refresh preview.**

---

## Implementation

### Backend / game
- No changes. Endpoints and authlib plumbing already exist.

### Files to ADD (in `src/Gml.Launcher`)
1. `Core/Services/ISkinUploadService.cs`
   - `Task<(bool ok, string? error)> UploadSkinAsync(string login, string accessToken, Stream file, string fileName, CancellationToken ct)`
   - `Task<(bool ok, string? error)> UploadCloakAsync(string login, string accessToken, Stream file, string fileName, CancellationToken ct)`
2. `Core/Services/SkinUploadService.cs`
   - One private `PostTextureAsync(string relativeEndpoint, login, accessToken, stream, fileName, ct)`
     that both public methods call with `api/v1/integrations/texture/skins/load` or
     `.../cloaks/load`.
   - Resolve the host from the resolved gateway: the DI registers `IGmlClientManager` as the
     concrete `GmlClientManager`, which exposes `public Uri HostUri`. Cast to it and build the
     absolute URL from `HostUri` (do NOT hardcode `ResourceKeysDictionary.Host` directly — go
     through the resolved `HostUri` so this keeps working if host resolution ever changes).
   - Send `MultipartFormDataContent`: a `StringContent` part named `Login` and a
     `StreamContent`/`ByteArrayContent` part named `Texture` (with a PNG content type), plus
     `Authorization: Bearer {accessToken}`.
   - Return a friendly error string on non-success status for the toast.

### Files to EDIT
3. `Core/Extensions/ServiceLocator.cs`
   - Register `new SkinUploadService(...)` as `ISkinUploadService`, same pattern as the other
     services.
4. `ViewModels/Pages/ProfilePageViewModel.cs`
   - Resolve/inject `ISkinUploadService` (optional ctor param + `Locator.Current` fallback,
     matching the existing convention).
   - Add `UploadSkinCommand` and `UploadCloakCommand` (`ReactiveCommand`). Each:
     1. Opens Avalonia `IStorageProvider.OpenFilePickerAsync` filtered to PNG.
     2. **Validates** with ImageSharp (already referenced): must be PNG and 64×64
        (accept legacy 64×32 for skins). On failure show `ShowError(...)`.
     3. Calls the matching upload service method with `LauncherUser.Name` +
        `LauncherUser.AccessToken`.
     4. On success: re-run `LoadData()` and force the preview to refresh (the view appends a
        cache-buster via `NoiseStringAddConverter` on `SkinUrl`, so re-assigning `TextureUrl`
        re-triggers `AsyncSkinRenderLoader`). Show `ShowSuccess(...)`.
5. `Views/Pages/ProfilePageView.axaml` + `ProfilePageView.axaml.cs`
   - Add two `GmlButton`s ("Change skin", "Change cloak") near the existing "Cabinet" button.
   - Code-behind exposes the `TopLevel` (window handle) so the VM can open the file picker —
     this is the one new bit of plumbing, since the page currently has no window reference.
     (Options: pass `TopLevel` into the command, or expose it via an interaction/handler.)
6. Localization
   - Add keys to `Assets/Resources/Resources.resx`, `Resources.ru.resx`, `Resources.en.resx`:
     `ChangeSkin`, `ChangeCloak`, `SkinUploaded`, `CloakUploaded`, `InvalidSkinFormat`.
   - Add matching constants in `Core/SystemConstants.cs` and reference via
     `ILocalizationService.GetString(...)`. No hardcoded UI strings.

---

## Test path
1. Build and run the launcher; log in.
2. Open the Profile page.
3. Upload a valid 64×64 PNG skin → preview updates; success toast.
4. Try an invalid file (wrong size / non-PNG) → error toast, no upload.
5. Upload a cloak the same way.
6. Launch the game; confirm the skin/cloak appears in a singleplayer world and on a server.

## Open/edge considerations
- Access token expiry: if upload returns 401/403, surface a "re-login" message.
- File size / dimension guard prevents a confusing backend rejection.
- Consider disabling the buttons while an upload is in flight (bind to the command's
  `IsExecuting`).

## Reference links
- Wiki: https://gml-launcher.ru/docs/gml-launcher/backend/installation/
- Backend: https://github.com/Gml-Launcher/Gml.Backend
- Web API (texture handler + routes): https://github.com/Gml-Launcher/Gml.Web.Api
- Launcher (upstream): https://github.com/Gml-Launcher/Gml.Launcher
- Web panel: https://github.com/Gml-Launcher/Gml.Web.Client
