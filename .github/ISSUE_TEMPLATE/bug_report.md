---
name: Bug report
about: A defect in the ProdFalcon API or scanner
title: "[Bug] "
labels: bug
---

## Summary

What failed, in one paragraph.

## Environment

- Commit SHA:
- OS:
- .NET SDK version (`dotnet --version`):
- SQL Server or test host (SQLite via `dotnet test`):
- API environment (`Development`, `Production`, or `Testing`):

## Steps to reproduce

1.
2.
3.

## Expected behavior

What the code or the documentation says should happen.

## Actual behavior

Status code, `traceId`, scan `status` and `stage`, and the error message. Do not paste JWTs, passwords, GitHub tokens, webhook secrets, or the contents of an untrusted archive.

## Logs

Relevant log lines with secrets removed.

## Isolation

- [ ] I can reproduce this on a current `master` build.
- [ ] This involves tenant data leaking across tenants.
- [ ] This involves ZIP extraction or path handling.
- [ ] This involves authentication or authorization.

If a tenant-isolation or archive-escape issue is unfixed, use the process in [SECURITY.md](../../SECURITY.md) instead of this public template.
