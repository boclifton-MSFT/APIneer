---
name: security-review
description: 'AI-powered codebase security scanner that reasons about code like a security researcher — tracing data flows, understanding component interactions, and catching vulnerabilities that pattern-matching tools miss.'
---

# Security Review

An AI-powered security scanner that reasons about your codebase the way a human security
researcher would — tracing data flows, understanding component interactions, and catching
vulnerabilities that pattern-matching tools miss.

## When to Use This Skill

Use this skill when the request involves:

- Scanning a codebase or file for security vulnerabilities
- Running a security review or vulnerability check
- Checking for SQL injection, XSS, command injection, or other injection flaws
- Finding exposed API keys, hardcoded secrets, or credentials in code
- Auditing dependencies for known CVEs
- Reviewing authentication, authorization, or access control logic
- Detecting insecure cryptography or weak randomness
- Performing a data flow analysis to trace user input to dangerous sinks
- Any request phrasing like "is my code secure?", "scan this file", or "check my repo for vulnerabilities"

## How This Skill Works

Unlike traditional static analysis tools that match patterns, this skill:
1. **Reads code like a security researcher** — understanding context, intent, and data flow
2. **Traces across files** — following how user input moves through your application
3. **Self-verifies findings** — re-examines each result to filter false positives
4. **Assigns severity ratings** — CRITICAL / HIGH / MEDIUM / LOW / INFO
5. **Proposes targeted patches** — every finding includes a concrete fix
6. **Requires human approval** — nothing is auto-applied; you always review first

## Execution Workflow

Follow these steps **in order** every time:

### Step 1 — Scope Resolution
Determine what to scan:
- If a path was provided, scan only that scope
- If no path given, scan the **entire project** starting from the root
- Identify the language(s) and framework(s) in use
- Read `references/language-patterns.md` to load language-specific vulnerability patterns

### Step 2 — Dependency Audit
Before scanning source code, audit dependencies first (fast wins):
- Check package manifests for known vulnerable packages
- Flag packages with known CVEs, deprecated crypto libs, or suspiciously old pinned versions
- Read `references/vulnerable-packages.md` for a curated watchlist

### Step 3 — Secrets & Exposure Scan
Scan ALL files (including config, env, CI/CD, Dockerfiles, IaC) for:
- Hardcoded API keys, tokens, passwords, private keys
- `.env` files accidentally committed
- Secrets in comments or debug logs
- Cloud credentials (AWS, GCP, Azure, Stripe, Twilio, etc.)
- Database connection strings with credentials embedded
- Read `references/secret-patterns.md` for regex patterns and entropy heuristics to apply

### Step 4 — Vulnerability Deep Scan
This is the core scan. Reason about the code — don't just pattern-match.
Read `references/vuln-categories.md` for full details on each category.

**Injection Flaws** — SQL Injection, XSS, Command Injection, LDAP, XPath, Header, Log injection

**Authentication & Access Control** — Missing auth, BOLA/IDOR, JWT weaknesses, session fixation, CSRF, privilege escalation, mass assignment

**Data Handling** — Sensitive data in logs, missing encryption, insecure deserialization, path traversal, XXE, SSRF

**Cryptography** — Weak algorithms (MD5, SHA1, DES), hardcoded IVs/salts, weak RNG

**Business Logic** — Race conditions (TOCTOU), integer overflow, missing rate limiting, predictable resource IDs

### Step 5 — Cross-File Data Flow Analysis
Trace user-controlled input from entry points to sinks. Identify vulnerabilities that only appear when looking at multiple files together.

### Step 6 — Self-Verification Pass
For EACH finding: re-read code, check for upstream sanitization, assign final severity.

### Step 7 — Generate Security Report
Output the full report in the format defined in `references/report-format.md`.

### Step 8 — Propose Patches
For every CRITICAL and HIGH finding, generate a concrete patch with before/after code.

## Severity Guide

| Severity | Meaning | Example |
|----------|---------|---------|
| 🔴 CRITICAL | Immediate exploitation risk, data breach likely | SQLi, RCE, auth bypass |
| 🟠 HIGH | Serious vulnerability, exploit path exists | XSS, IDOR, hardcoded secrets |
| 🟡 MEDIUM | Exploitable with conditions or chaining | CSRF, open redirect, weak crypto |
| 🔵 LOW | Best practice violation, low direct risk | Verbose errors, missing headers |
| ⚪ INFO | Observation worth noting, not a vulnerability | Outdated dependency (no CVE) |

## Reference Files

Load as needed:
- `references/vuln-categories.md` — Deep reference for every vulnerability category
- `references/secret-patterns.md` — Regex patterns, entropy-based detection, CI/CD secret risks
- `references/language-patterns.md` — Framework-specific vulnerability patterns
- `references/vulnerable-packages.md` — Curated CVE watchlist
- `references/report-format.md` — Structured output template for security reports
