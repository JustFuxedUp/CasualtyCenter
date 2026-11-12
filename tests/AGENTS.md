# Regression-check guidance

Inherits the root AGENTS.md. `ProcedureChecks.csproj` is a standalone .NET 10 console runner linking production rule/adapter files. GameStubs are deliberately minimal; they do not run Unity, native hand physics, serialization hooks in the actual host, audio or network behavior.

Add tests for behavior and failure boundaries: invalid values; wrong patient/limb; partial save/restore; duplicate completion; external health changes; interrupted incision debt; cross-limb isolation; infection setting/coverage; gel dose thresholds. Prefer production methods over a copied algorithm in the test.

Keep stubs narrow. A new stubbed field is not proof that the game has that field: verify the installed assembly first. When adding a linked production file, update the test project includes and keep test files excluded from the plugin project.

`SURGERY_AUDIO_TEST_DIR` optionally decodes supplied WAVs; those checks are not a listening test and the files are not required in Git. Counts differ when that variable is absent. Report the actual output, not a historical number.

Do not build a parallel simulation of Unity to test a visual constant. For UI changes, build and provide a targeted real-game playtest. Do not rerun the suite repeatedly after an unchanged passing result.
