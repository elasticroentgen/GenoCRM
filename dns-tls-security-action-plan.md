# DNS & TLS Security Hardening — Action Plan

**Owner:** Markus Keil (DevOps)
**Date:** 2026-04-15
**Audience:** Management / Security stakeholders
**Source tickets:** ETH-1722, ETH-1723, ETH-1724, ETH-1725, ETH-1727, ETH-1728, ETH-1729

---

## 1. Executive summary

We are running a coordinated hardening pass across our DNS and TLS surface. The goal is to close gaps that today depend on trust in upstream providers (registrars, CAs, DNS hosts) and replace that trust with cryptographic guarantees and automated reconciliation we control.

The work breaks into three streams:

1. **DNS integrity** — make sure the records that resolvers see are exactly what we declared in IaC, signed, and not silently mutable upstream.
2. **Certificate issuance control** — ensure only *we* can get certificates for our domains, only via the methods we use, and only from the accounts we own.
3. **Recovery & resilience** — keep working through key loss, host loss, or provider compromise, given our 7-day Let's Encrypt cert posture (which leaves only ~4 days of renewal slack).

None of this is greenfield. It builds on assets we already operate (Terraform-managed zones, Semaphore CI, OpenBao, gitops-runner, certbot on `colo-lb-0`).

---

## 2. Current state

### DNS

- **Authoritative DNS is multi-provider.** Records are pushed to **AWS Route 53** and **PowerDNS** in parallel; both are listed at the registrar. A single-provider outage does not take us down.
- **Zones are deployed via IaC.** The `ethquokkaops/dns/terraform` workspace defines every zone. Provider credentials (Cloudflare, Route 53, OVH, PowerDNS) are wired through OpenBao into the `gitops-runner` Semaphore environment.
- **Apply is git-push triggered only.** Semaphore webhooks (`trigger_tofu_infra.tf`) run a `tofu apply` when `main` moves. There is **no periodic reconciliation** — out-of-band changes at the providers persist until someone notices.
- **Semaphore's tofu wrapper cannot auto-approve**, which is why scheduled reconciliation needs a separate bash-app template (see ETH-1722).
- **DNSSEC is not enabled** on our zones. Multi-provider serving with independent signers is incompatible with DNSSEC's single-DS-record chain of trust, so today's redundancy posture has been the blocker.

### Registrars

- **User access at the registrars is deliberately limited** to a small named group. This is good baseline posture but is **not a substitute** for cryptographic controls: a registrar account compromise, a registrar-side bug, or an upstream BGP/CA event can still introduce records or certificates we did not authorize. The work below assumes registrar access is tight and adds defenses that survive a registrar incident anyway.

### Certificates

- **Let's Encrypt across the fleet, 7-day certs.** Renewal slack is ~4 days — any control we add (CAA pinning, DNSSEC) that can break renewal is a near-term outage risk if misconfigured.
- **Each certbot host effectively runs its own ACME account.** No consolidation by environment.
- **No off-host backup of `/etc/letsencrypt/`** today on `colo-lb-0`. Loss of the account key means loss of revocation capability and (once CAA pinning lands) a renewal outage.
- **CAA is either absent or only restricts CA, not account or method.** Any LE customer can technically issue for us via any challenge type.
- **No Certificate Transparency monitoring in production.** A self-hosted `certstream-server-go` + Python firehose exists in `cert-stream/` but only runs locally and writes to stdout.
- **Cloudflare-proxied properties** are out of scope for account-level pinning — CF rotates between Google Trust Services / LE / SSL.com / DigiCert by design.
- **Netlify-hosted sites** today rely on Netlify's own LE issuance via HTTP-01, which is incompatible with strict account-pinned CAA. Bring-your-own-cert (BYOC) automation is required for any Netlify site we want under our pinning.

---

## 3. Risk landscape this addresses

| Risk | Today's exposure | Mitigation in this plan |
| -- | -- | -- |
| Out-of-band DNS change (manual edit, hijacked provider account) | Persists until noticed | ETH-1722 — 15-min drift detection + auto-remediation |
| Spoofed DNS responses (cache poisoning, on-path attacker) | Undermines CAA, breaks bootstrap HTTPS for non-preloaded hosts | ETH-1724 — DNSSEC |
| Rogue cert issued for our domain | Possible; we have no visibility | ETH-1723 — CT-log monitor in production |
| Any LE customer issuing for our domains | Allowed by current CAA posture | ETH-1729 — `accounturi=` + `validationmethods=` pinning |
| Account-key loss → revocation lost, renewals broken | Real (no backups) | ETH-1725 — encrypted off-host backups; ETH-1728 — fewer keys to safeguard |
| CAA pinning churn / unmaintainability | One CAA per host doesn't scale | ETH-1728 — one ACME account per security boundary |
| MITM via downgraded/forged cert chain on critical endpoints | Browser CA trust only | ETH-1727 — TLSA / DANE evaluation |

---

## 4. Workstreams and dependencies

The streams are intentionally sequenced so that the highest-leverage controls land on a foundation that can support them.

```
                        ┌──────────────────────────────┐
                        │ ETH-1722  DNS drift detection│   (independent — start now)
                        └──────────────────────────────┘

  ┌─────────────────────┐        ┌──────────────────────────────┐
  │ ETH-1725  Backups   │  ◄──── │ ETH-1728  ACME consolidation │
  └─────────────────────┘        └──────────────┬───────────────┘
                                                │
                                                ▼
                                 ┌──────────────────────────────┐
                                 │ ETH-1729  CAA accounturi pin │
                                 └──────────────┬───────────────┘
                                                │ benefits from
                                                ▼
                                 ┌──────────────────────────────┐
                                 │ ETH-1724  DNSSEC rollout     │   (parallel with 1729)
                                 └──────────────────────────────┘

                        ┌──────────────────────────────┐
                        │ ETH-1723  CT monitoring      │   (parallel; see §5.2)
                        └──────────────────────────────┘

                        ┌──────────────────────────────┐
                        │ ETH-1727  TLSA/DANE          │   (research; depends on DNSSEC)
                        └──────────────────────────────┘
```

---

## 5. The plan

### 5.1 Stream A — DNS integrity

**ETH-1722 — DNS drift detection with auto-remediation (Priority: High)**

- Every 15 minutes, a Semaphore bash template iterates every workspace under `ethquokkaops/dns/terraform/environments/`, runs `tofu plan -detailed-exitcode`, and on drift posts to Mattermost, auto-applies, and posts the result.
- Reuses the gitops-runner (already has state, Vault token, all provider creds) and bypasses the interactive tofu wrapper by shelling directly to `/usr/bin/tofu`.
- Net effect: any out-of-band change in Cloudflare / Route 53 / OVH / PowerDNS is rolled back within 15 minutes and visible to DevOps in real time.
- **Risk to manage:** provider rate limits at 15-min cadence across 40+ workspaces (OVH is the tightest). Cadence is tunable.

**ETH-1724 — DNSSEC rollout (single-signer with signed zone transfer)**

- Switch from "two providers each authoritative and unsigned" to "PowerDNS signs, Route 53 receives signed AXFR, one DS at the registrar."
- Trade-off accepted: if PowerDNS is fully down before a transfer completes, Route 53 serves stale-but-valid signatures until RRSIG expiry. Acceptable for our redundancy posture.
- Multi-signer (RFC 8901) is the architecturally cleaner answer but adds significant ops complexity — deferred as a follow-up.
- Phased rollout: lower TTLs → enable signing → set up signed transfers → publish DS → stabilize. Start on a non-critical zone.
- **Hard constraint:** do not publish DS on a production zone within 4 days of a known cert renewal — a misconfigured DS breaks ACME along with everything else.

### 5.2 Stream B — Certificate issuance control

**ETH-1728 — Consolidate Let's Encrypt ACME accounts by security boundary** *(blocks 5.2's CAA work)*

- Today: one ACME account per certbot host. Tomorrow: one canonical account per security boundary (prod, staging, isolated cases).
- Prod boundary inherits the existing `colo-lb-0` account to preserve rate-limit history.
- Distribution of the canonical account key uses our secrets-management path, not ad hoc tarball copies.
- **Why this is the gate for everything else CAA-related:** without it, `accounturi=` pinning means one CAA value per host — unmaintainable.
- Includes documenting the ACME `keyChange` rotation procedure as our cheap "host got popped" recovery (does not require touching CAA).

**ETH-1729 — Pin CAA records with `accounturi=` and `validationmethods=` for non-CF domains** *(blocked by ETH-1728)*

- Move from "any LE customer can issue for us" to "only our specific account, only via the methods we actually use (typically dns-01)."
- Per-boundary: prod domains pin the prod account URI; staging pins staging.
- **Out of scope:** Cloudflare-proxied domains (CF rotates CAs by design — the loose-CAA fallback is documented in ETH-1723).
- **Risk to manage:** a wrong `accounturi=` value hard-blocks renewal within ~4 days. Stage on one low-risk domain first; verify a renewal succeeds end-to-end before rolling.

**ETH-1723 — CT-log monitoring + CAA hardening (umbrella ticket)**

- Productionize the existing `cert-stream/` PoC: move to a small VM, route hits to a real channel (Mattermost/Slack), maintain a fingerprint allowlist fed from our own renewal hooks, persist hits to SQLite for dedup.
- Tier alerts: P1 = cert from a CA not in our CAA list → likely rogue issuance. P2 = cert from allowed CA but unknown fingerprint → possible account compromise OR missed deploy hook. P3 = lookalike domain → hand to abuse team.
- Includes the **Netlify BYOC pipeline**: for sites we want under strict CAA, issue via our pinned LE account using dns-01 and push to Netlify via their SSL API with a certbot deploy-hook. For static / low-risk Netlify sites, accept a per-name loose-CAA fallback and let CT-stream carry more of the load there.
- This ticket is the umbrella; ETH-1728 / ETH-1729 are its first concrete steps.

### 5.3 Stream C — Recovery & resilience

**ETH-1725 — Automate certbot account/config backup to S3, GPG-encrypted** *(related to ETH-1728)*

- Weekly systemd timer on `colo-lb-0` (and any other certbot host found during ETH-1728 inventory) tars `/etc/letsencrypt/` preserving permissions, GPG-encrypts to a dedicated backup pubkey, and uploads to our existing backup bucket (SSE + versioning + lifecycle already in place).
- IAM scoped to `s3:PutObject` on the prefix — **write-only**, no read/list/delete from the source host.
- GPG private key held offline / in secrets management, never on the host being backed up.
- One restore drill required as part of acceptance, runbook committed.
- Alerting: page if no new object lands in the prefix for >8 days.
- After ETH-1728 completes, the backup target is the consolidated per-boundary account key — that's what really matters.

### 5.4 Stream D — Research / future

**ETH-1727 — TLSA / DANE evaluation**

- Currently a placeholder ticket. TLSA pins our cert (or its issuing CA) in DNS, validated via DNSSEC, giving a DNS-based alternative to (or complement to) browser CA trust.
- **Hard-blocks on DNSSEC (ETH-1724)** — TLSA without DNSSEC is meaningless.
- Action: produce a short evaluation document covering applicability per service (mail/MTA-STS, web with limited browser support, internal services), operational cost of cert/key rollovers under TLSA, and a recommendation.

---

## 6. Sequencing recommendation

| Order | Item | Rationale |
| -- | -- | -- |
| 1 (now, parallel) | ETH-1722 drift detection | Standalone, plugs a current visibility gap, low blast radius. |
| 1 (now, parallel) | ETH-1725 backups | Standalone, removes the "one host away from outage" risk before we add CAA pinning that depends on the account key. |
| 1 (now, parallel) | ETH-1723 CT monitor productionization | Standalone, gives detection coverage immediately even before issuance is locked down. |
| 2 | ETH-1728 ACME consolidation | Required gate for CAA pinning. |
| 3 (parallel) | ETH-1729 CAA `accounturi=` pinning | Highest-leverage cert hardening once accounts are consolidated. |
| 3 (parallel) | ETH-1724 DNSSEC rollout | Strengthens CAA (prevents spoofing) and unblocks TLSA. Phased per zone. |
| 4 | ETH-1727 TLSA evaluation | Begins as research after DNSSEC is stable on at least one zone. |

---

## 7. What this plan does NOT cover

- Lookalike-domain takedowns (separate workflow with abuse contacts / registrars).
- Internal PKI / private CAs.
- Code-signing certificates.
- Cloudflare-managed DNSSEC (CF handles signing for zones it's authoritative for; our scope is the zones we sign ourselves).
- Coordinating Cloudflare-proxied properties into account-level CAA pinning (incompatible by design).
- Multi-signer DNSSEC (RFC 8901) — explicit deferred follow-up to ETH-1724.

---

## 8. Decisions needed from management

1. **Mattermost channel** for DNS drift alerts (proposed: new `#devops-dns-drift`) and for CT-log hits (proposed: existing security channel).
2. **Per-Netlify-site tier decision** (BYOC vs loose-CAA) once the inventory in ETH-1723 is produced — this is a per-property risk call (auth/wallet-adjacent vs static marketing).
3. **Acceptance** that during DNSSEC rollout we have a hard rule: no DS publication within 4 days of a known cert renewal window without explicit go-ahead.
4. **Acceptance** of the single-signer DNSSEC trade-off (stale-but-valid signatures from secondary if primary is fully down before transfer) versus the operational cost of a multi-signer setup.
