# Contributing

Issues and focused pull requests are welcome.

## Reporting bugs

Use the bug-report issue form and include:

- KSP and Kerbal Proportions versions;
- a minimal reproduction sequence;
- whether the affected model is EVA or IVA;
- relevant suit, texture, IVA, or animation mods; and
- the relevant portion of `KSP.log`.

Do not upload saves or logs containing information you do not want to share
publicly.

## Development

1. Fork and clone the repository.
2. Keep changes narrowly scoped and avoid committing compiled DLLs, KSP
   assemblies, settings, or personal profiles.
3. Build against a legally obtained local KSP 1.12.5 installation:

   ```powershell
   .\build.ps1 -KspRoot 'C:\Path\To\Kerbal Space Program'
   ```

4. Test both a stock EVA Kerbal and a seated IVA Kerbal when changing discovery,
   matching, transform application, or camera behavior.
5. Explain behavior changes and verification in the pull request.

Code must remain compatible with the compiler and Unity/.NET profile shipped
with KSP 1.12.5. Do not redistribute KSP or Unity assemblies.
