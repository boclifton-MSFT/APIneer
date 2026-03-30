# Secret & Credential Detection Patterns

## High-Confidence Secret Patterns

### API Keys & Tokens
```regex
sk-[a-zA-Z0-9]{48}                          # OpenAI
sk-ant-[a-zA-Z0-9\-_]{90,}                  # Anthropic
AKIA[0-9A-Z]{16}                             # AWS Access Key
gh[pousr]_[a-zA-Z0-9]{36,}                  # GitHub Token
github_pat_[a-zA-Z0-9]{82}                  # GitHub PAT
sk_live_[a-zA-Z0-9]{24,}                    # Stripe
SG\.[a-zA-Z0-9\-_.]{66}                     # SendGrid
xoxb-[0-9]+-[0-9]+-[a-zA-Z0-9]+             # Slack Bot
AIza[0-9A-Za-z\-_]{35}                      # Google API Key
key-[a-zA-Z0-9]{32}                          # Mailgun
```

### Private Keys
```regex
-----BEGIN (RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY( BLOCK)?-----
```

### Database Connection Strings
```regex
mongodb(\+srv)?:\/\/[^:]+:[^@]+@
(postgres|postgresql|mysql):\/\/[^:]+:[^@]+@
redis:\/\/:[^@]+@
```

## Entropy-Based Detection
- Shannon entropy > 4.5 bits/char AND > 20 chars AND assigned to a variable = likely secret
- Exclude: Lorem ipsum, HTML/CSS, UUID/GUID

## Files That Should Never Be Committed
`.env`, `.env.local`, `*.pem`, `*.key`, `*.p12`, `id_rsa`, `credentials.json`, `service-account.json`, `secrets.yaml`

## CI/CD & IaC Risks
- GitHub Actions: hardcoded values in `env:` blocks, printing `${{ secrets.* }}`
- Docker: secrets in `ENV` or `ARG` (persisted in layers)
- Terraform: hardcoded `password` or `access_key` values

## Safe Patterns (Do NOT flag)
`"your-api-key-here"`, `"<YOUR_API_KEY>"`, `"${API_KEY}"`, `"REPLACE_WITH_YOUR_KEY"`, `"sk-..."` in documentation
