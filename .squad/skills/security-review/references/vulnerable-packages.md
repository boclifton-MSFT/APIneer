# Vulnerable & High-Risk Package Watchlist

## npm / Node.js

| Package | Vulnerable Versions | Issue | Safe Version |
|---------|-------------------|-------|--------------|
| lodash | < 4.17.21 | Prototype pollution | >= 4.17.21 |
| axios | < 1.6.0 | SSRF, open redirect | >= 1.6.0 |
| jsonwebtoken | < 9.0.0 | Algorithm confusion bypass | >= 9.0.0 |
| tar | < 6.1.9 | Path traversal | >= 6.1.9 |
| express | < 4.19.2 | Open redirect | >= 4.19.2 |
| vm2 | ANY | Sandbox escape (deprecated) | Use isolated-vm |
| node-fetch | < 2.6.7 | Open redirect | >= 2.6.7 or 3.x |

## Python / pip

| Package | Vulnerable Versions | Issue | Safe Version |
|---------|-------------------|-------|--------------|
| Pillow | < 10.0.1 | Multiple CVEs | >= 10.0.1 |
| cryptography | < 41.0.0 | OpenSSL vulnerabilities | >= 41.0.0 |
| PyYAML | < 6.0 | Arbitrary code via yaml.load() | >= 6.0 |
| requests | < 2.31.0 | Proxy auth info leak | >= 2.31.0 |
| Django | < 4.2.16 | Various | >= 4.2.16 |

## Java / Maven

| Package | Vulnerable Versions | Issue |
|---------|-------------------|-------|
| log4j-core | 2.0-2.14.1 | Log4Shell RCE (CVE-2021-44228) — CRITICAL |
| Spring Framework | < 5.3.28 | Various CVEs |
| Jackson-databind | < 2.14.0 | Deserialization |

## General Red Flags

1. Not updated in > 2 years with > 10 open security issues
2. Deprecated by maintainer with security advisory
3. Name one character off from popular package (typosquatting)
4. Recently transferred to new owner
