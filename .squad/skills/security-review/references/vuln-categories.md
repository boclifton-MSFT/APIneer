# Vulnerability Categories — Deep Reference

## 1. Injection Flaws

### SQL Injection
- String concatenation/interpolation in SQL queries
- Raw `.query()`, `.execute()`, `.raw()` with variables
- ORM `whereRaw()`, `selectRaw()` with user input
- Second-order SQLi: data stored safely, used unsafely later

### XSS
- `innerHTML`, `outerHTML`, `document.write()` with user data
- `dangerouslySetInnerHTML` (React), `v-html` (Vue), `bypassSecurityTrustHtml` (Angular)
- DOM-based: `location.hash`, `document.referrer` written to DOM

### Command Injection
- `exec(userInput)`, `spawn('sh', ['-c', userInput])` (Node.js)
- `os.system(user_input)`, `subprocess.call(input, shell=True)` (Python)

### SSRF
- HTTP requests where URL is user-controlled
- Webhooks, URL preview, image fetch features
- High-risk targets: `169.254.169.254`, `localhost`, internal IPs

## 2. Authentication & Access Control

### BOLA / IDOR
- Resource IDs from URL/params without ownership check
- `findById(req.params.id)` without verifying ownership

### JWT Vulnerabilities
- `alg: "none"` accepted, weak/hardcoded secrets
- No expiry validation, algorithm confusion attacks
- JWT in `localStorage` (XSS risk)

### CSRF
- State-changing operations without CSRF token
- Missing `SameSite` cookie attribute

## 3. Secrets & Sensitive Data
- Hardcoded API keys, tokens, passwords
- Secrets in logs, error messages, API responses
- Stack traces exposed to users

## 4. Cryptography
| Weak | Replace With |
|------|--------------|
| MD5, SHA-1 | SHA-256 or bcrypt |
| DES / 3DES | AES-256-GCM |
| Math.random() | crypto.randomBytes() |

## 5. Business Logic
- Race conditions (TOCTOU) — use atomic transactions
- Missing rate limiting on auth/email/SMS endpoints
- Predictable resource identifiers

## 6. Path Traversal
- User-controlled filename in file read/write operations
- Mitigation: `os.path.basename()` + prefix validation
