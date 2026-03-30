# Security Report Format

## Header
```
╔══════════════════════════════════════════════════════════╗
║           🔐 SECURITY REVIEW REPORT                     ║
╚══════════════════════════════════════════════════════════╝

Project: <project name>
Scan Date: <today's date>
Scope: <files/directories scanned>
Languages Detected: <list>
```

## Executive Summary Table
```
┌────────────────────────────────────────────────┐
│           FINDINGS SUMMARY                     │
├──────────────┬─────────────────────────────────┤
│ 🔴 CRITICAL  │  <n> findings                  │
│ 🟠 HIGH      │  <n> findings                  │
│ 🟡 MEDIUM    │  <n> findings                  │
│ 🔵 LOW       │  <n> findings                  │
│ ⚪ INFO      │  <n> findings                  │
├──────────────┼─────────────────────────────────┤
│ TOTAL        │  <n> findings                  │
└──────────────┴─────────────────────────────────┘
```

## Finding Card Format
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[SEVERITY EMOJI] [SEVERITY] — [VULNERABILITY TYPE]
Confidence: HIGH / MEDIUM / LOW
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📍 Location:  file, Line N
🔍 Vulnerable Code: (snippet)
⚠️  Risk: (plain English explanation + example attack)
✅ Recommended Fix: (concrete code change)
📚 Reference: OWASP reference
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

## Confidence Ratings

| Confidence | When to Use |
|------------|-------------|
| **HIGH** | Vulnerability is unambiguous. Exploitable as-is. |
| **MEDIUM** | Likely exists but depends on runtime context. |
| **LOW** | Suspicious pattern, could be false positive. |
