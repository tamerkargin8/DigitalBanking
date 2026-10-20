# DigitalBanking collaboration rules

Read docs/LOCAL_DEVELOPMENT.md and docs/DEEPSEEK_HANDOFF.md first. Repository state and verified commands are the source of truth; old refactoring reports are historical.

GPT owns the integration/local-development branch and integration decisions. DeepSeek works in the separate DigitalBanking-DeepSeek worktree on deepseek/dashboard-and-tests. Never edit the other worktree, switch its branch, or merge your own work into the integration branch.

Initial DeepSeek scope: inspect the baseline, then add meaningful dashboard/API contract tests and propose UI improvements. Keep authentication, payments, migrations, persistence, package versions, and security configuration unchanged unless the user explicitly expands scope. Record out-of-scope findings in docs/DEEPSEEK_FINDINGS.md.

Keep money as decimal. SQLite is a disposable Development-only playground, not proof of SQL Server behavior. Do not change existing SQL Server migrations or convert money to floating point to satisfy SQLite.

Do not inspect, print, commit, or transmit .local credentials, tokens, private connection strings, or account information. Local launchers and tests may consume these values internally without displaying them. Work only with synthetic banking data. Do not contact live banking/payment systems. Never push, publish, deploy, or install unrelated dependencies without user instruction. Treat these as project conventions; the user's explicit instructions take precedence.

Use bash scripts/dotnet.sh build DigitalBanking.slnx and python3 scripts/run_local.py. For a second concurrent app instance use --api-port 5180 --dashboard-port 5181. Verify changes with relevant tests and report exact results. Never claim an unrun check passed. Preserve unrelated user changes.
