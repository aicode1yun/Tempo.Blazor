# Keycloak realm — Tempo Report Server (Fáze 4)

`tempo-reports-realm.json` is a re-importable Keycloak realm export for the Tempo Report
Server. It was produced with the admin `partial-export` (clients + roles + client scopes),
then augmented with the dev test users and sanitized so **no client secret is committed**.

## What the realm contains

- Realm `tempo-reports`, `accessTokenLifespan = 300` s.
- Realm roles (capability ceiling): `report-admin`, `report-author`, `report-viewer`.
- Clients:
  - `tempo-report-api` — bearer-only resource client. Owns the client role `report.render`.
  - `tempo-report-web` — confidential BFF client. Standard flow + PKCE (S256), direct grants
    enabled (dev token verification), redirect/web-origins for `https://localhost:7150` and
    `http://localhost:5000`, post-logout redirect URIs.
  - `tempo-report-m2m` — confidential service-account client (client credentials).
- Client scope `tempo-report-api-audience` with an **Audience** mapper
  (`included.client.audience = tempo-report-api`) assigned as a default scope to
  `tempo-report-web` and `tempo-report-m2m`, so their access tokens carry `aud=tempo-report-api`.
- Test users (all password `Pass123!`, non-temporary): `admin1` (report-admin),
  `author1` (report-author), `viewer1` (report-viewer).
- The `tempo-report-m2m` service account is granted the `report.render` client role.

## Secrets

Client secrets are replaced with placeholders and MUST be supplied at import time:

- `${TEMPO_REPORT_WEB_SECRET}` — secret for `tempo-report-web`.
- `${TEMPO_REPORT_M2M_SECRET}` — secret for `tempo-report-m2m`.

Dev secrets are kept out of git (use user-secrets / environment):

```
Authentication:Oidc:ClientSecret = <tempo-report-web secret>   # Web host
```

## Import (dev)

The bundled `kcadm.bat -f <file>` / `-f -` (stdin) path is unreliable on Windows
(BOM / stream issues); use the Admin REST API instead. Substitute the secret placeholders
first (e.g. `sed`/`envsubst`), then:

```powershell
$at = (Invoke-RestMethod -Method Post -Uri "http://localhost:8080/realms/master/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ grant_type="password"; client_id="admin-cli"; username="admin"; password="admin" }).access_token
$body = [System.Text.Encoding]::UTF8.GetBytes([System.IO.File]::ReadAllText("tempo-reports-realm.json"))
Invoke-RestMethod -Method Post -Uri "http://localhost:8080/admin/realms" `
  -Headers @{ Authorization = "Bearer $at" } -ContentType "application/json" -Body $body
```

> Note: importing a realm whose JSON contains a *partial* `clientScopes` array replaces the
> built-in scopes (`basic`, `roles`, `profile`, `email`, …) and strips `sub`/roles from tokens.
> This export includes the full built-in scope set, so re-import is faithful.
