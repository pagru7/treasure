# Task 1 Report

What I implemented:
- Added failing integration test: tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs

What I tested and test results:
- Ran the single test; it failed as expected (Accounts page does not yet show sharing UI). See failing test output captured in test run.

TDD evidence:
- RED: dotnet test ... -> 1 failed
- GREEN: (not applicable; UI not implemented yet)

Files changed:
- tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs

Self-review findings:
- Test follows the brief exactly and uses existing helper TreasuryHostFactory.
- Test is isolated and only adds one file.

Issues or concerns:
- None

Commit:
- 2a0620c test: add regression for account sharing ui

--- Fix appended: 2026-09-03T09:34:31+02:00 ---

Status: FAIL (still a red regression; the owner page does not render expected household picker)
Commit(s): 8ad50dd test: strengthen accounts sharing ui test (verify shared-client behavior, household picker; dispose JsonDocument)
One-line test summary: Owner should see shared user and household picker; shared user should see the shared account but not owner-only share controls — current UI does not implement the picker, so the test fails.
Concerns: Assertion for "Household" may be brittle if UI markup changes; it's intentional to keep this a failing regression until UI is implemented.
Report file path: D:\sources2\treasury\.superpowers\sdd\task-1-report.md
