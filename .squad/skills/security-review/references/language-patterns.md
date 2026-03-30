# Language-Specific Vulnerability Patterns

## JavaScript / TypeScript (Node.js, React, Next.js, Express)

### Critical APIs to flag
```js
eval()                    // arbitrary code execution
Function('return ...')   // same as eval
child_process.exec()     // command injection if user input reaches it
fs.readFile              // path traversal if user controls path
```

### Express.js specific
- Missing helmet (security headers)
- Body size limits missing (DoS)
- CORS misconfiguration: `cors({ origin: '*' })`
- Trust proxy without validation

### React specific
```jsx
<div dangerouslySetInnerHTML={{ __html: userContent }} />  // XSS
<a href={userUrl}>link</a>  // javascript: URL injection
```

## Python (Django, Flask, FastAPI)

### Django: Raw SQL, missing CSRF, DEBUG=True, weak SECRET_KEY, ALLOWED_HOSTS=['*']
### Flask: debug=True, weak secret_key, eval with user input, SSTI via render_template_string
### FastAPI: Missing auth dependency, arbitrary file read via path traversal

## Java (Spring Boot)

- SQL injection via string concatenation in queries
- XXE via DocumentBuilderFactory without disallow-doctype-decl
- Deserialization of untrusted input
- Actuator endpoints exposed: `management.endpoints.web.exposure.include=*`

## Go

- Command injection via `exec.Command("sh", "-c", userInput)`
- SQL injection via string concatenation
- Path traversal via unsanitized `filepath.Join`
- Insecure TLS: `InsecureSkipVerify: true`

## Rust

- Unsafe blocks — flag for manual review
- Integer overflow in release builds (silently wraps)
- Unwrap/expect in production code (panics)
