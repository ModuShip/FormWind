# Security policy

## Supported versions

Security fixes are made for the latest released minor version. Before 1.0.0, only the latest preview is supported.

| Version | Supported |
| --- | --- |
| latest 0.x preview | ✅ |
| older previews | ❌ |

## Reporting a vulnerability

Please do **not** open a public issue for security problems. Report them privately through GitHub's
[private vulnerability reporting](https://github.com/ModuShip/FormWind/security/advisories/new)
(*Security* tab → *Report a vulnerability*).

Include a description of the issue, the affected version and target framework, steps or a minimal project that
reproduces it, and the impact you expect. Reports are handled privately; once a fix is available, a security advisory
is published and the reporter is credited unless they prefer otherwise.

## Scope

FormWind is a UI styling library that runs inside the host application's process. Relevant issues include, for example,
code execution or file access through theme files (`tw.config.json`, SVG icons, bundled fonts), crashes that can be
triggered by untrusted data shown in controls, and problems in the tools shipped with the repository (converter,
harness, documentation generator). Theme files and fonts are trusted application assets; do not load them from
untrusted sources.
