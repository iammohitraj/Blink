# Security policy

## Supported versions

| Version | Supported |
|---|---|
| 1.1.x | yes |
| 1.0.x | yes, security fixes only |
| older | no |

## Reporting a vulnerability

Please do **not** open a public issue for a security problem.

Use GitHub's private vulnerability reporting on the
[Security tab](https://github.com/iammohitraj/Blink/security/advisories/new) of the
repository. Include:

- A description of the issue and its impact.
- Steps to reproduce, or a proof of concept.
- Affected version(s) and Windows version.
- Any suggested fix, if you have one.

You can expect an initial acknowledgement within a few days. Once the issue is
confirmed, a fix will be prepared and a release published before the details are
disclosed. Credit will be given in the release notes unless you prefer otherwise.

## Scope

Blink installs per user, runs without elevation and makes network requests only to
`api.github.com` and the release download URLs it returns. Reports that involve
elevation, arbitrary code execution, tampering with the update payload, or leaking
local data outside the user's profile are especially relevant.

The following are out of scope:

- Issues that require an already-compromised machine or administrator access.
- Cosmetic problems in the installer or UI.
- Reports about third-party tools used to build or publish releases.
