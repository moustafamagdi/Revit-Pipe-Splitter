# Repository instructions

- Make all future Pipe Splitter source changes in this repository: C:\Users\m.abdellatif\Documents\GitHub\Revit Pipe Splitter.
- Do not edit or maintain copies in the earlier Codex outputs folders.
- Build one fixed output: PipeSplitter2024.dll in the repository root. Overwrite it on subsequent builds.
- Do not generate version-suffixed DLLs, versioned delivery folders, or versioned ZIP packages unless explicitly requested.
- Keep the installer and documentation pointing to PipeSplitter2024.dll.
- If the DLL is locked by Revit, report the lock and request that Revit be closed; do not work around it by producing a differently named DLL.
- Keep the UI in English.
- Do not run or test the DLL inside Revit; the user performs that testing. Compilation and standalone calculation checks are permitted.
