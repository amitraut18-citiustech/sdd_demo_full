Build this feature: $ARGUMENTS

1. Use the `fixer` subagent with the exact file-by-file change spec provided to implement the change.
2. Use the `verifier` subagent to build the project (backend: `dotnet build`; frontend: `npm run build` —
   this repo has no test suite, so a clean build is the pass/fail signal, not `dotnet test`/`npm run test`).
3. If the verifier reports FAIL, send the error back to `fixer` once to retry, then re-verify.
   If it still fails, stop and summarize for me.
4. When the build passes, give me a 3-line summary: files changed, what changed, build result.
