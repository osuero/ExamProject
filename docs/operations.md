# Operations

## Configuration (environment variables; `__` separates sections)
| Key | Meaning | Default |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection (secret) | required |
| `Database__MigrateOnStartup` | apply EF migrations at start | false (true in Development/compose) |
| `Content__Directory`, `Content__BootstrapOnStartup` | load catalog, sources, banks and review records idempotently | off |
| `Auth__PublicBaseUrl` | base URL used in sign-in links | http://localhost:4200 |
| `Auth__AdminEmails__N` | addresses promoted to Admin on sign-in | none |
| `Auth__TokenMinutes`, `Auth__MaxLinksPerEmailPer15Min` | link lifetime and per-address throttle | 15, 3 |
| `RateLimits__AuthPerMinute` | per-IP limit for request-link/verify | 10 |
| `Email__Mode` | `smtp` (any non-development environment) or `outbox` (Development/Testing only; startup fails otherwise) | smtp |
| `Email__SmtpHost/Port/StartTls/User/Password`, `Email__From` | SMTP provider (user/password are secrets) | — |
| `Security__CookieSecurePolicy` | `Always` (default outside dev) or `SameAsRequest` for local http only | Always |
| `DataProtection__KeysPath` | persistent directory for cookie/antiforgery keys | unset (in-memory) |
| `Workers__Expiry` | background expiry sweep | true |

Behind a reverse proxy, the app honours `X-Forwarded-For/Proto` from loopback proxies only (ASP.NET Core default). Configure `ForwardedHeadersOptions.KnownNetworks` for your proxy before relying on `Request.IsHttps` or client IPs for rate limiting.

## Deploy (not performed; needs the owner's choice of host)
1. Build the image: `docker build -t examprep .` (frontend is built into `wwwroot`; banks are copied to `/content`, outside the web root).
2. Provide PostgreSQL 16, the secrets above and a persistent volume for `/keys`.
3. Start with `Database__MigrateOnStartup=true` and `Content__BootstrapOnStartup=true` for the first run.
4. Check `GET /api/health` → `{"status":"ok","database":true}`.

## Content operations
- Add questions: Admin → Import (preview, then import) or put a file in `content/banks/` with a higher prefix and restart.
- Review: blind-solve with the `calidad` agent, then publish item by item in Admin, or add a review record to `content/reviews/` (hash-bound).
- Fix a published question: Admin → question → Edit (new draft version) → review → publish; the old version is retired, finished attempts keep showing what was answered.
- Withdraw: move to `quarantined` or `retired` with a note.
- Language: verify only with an official exam or provider source for that exam.

## Data requests
A user can delete their account and attempts from My exams. Admin audit entries keep only the user id.
