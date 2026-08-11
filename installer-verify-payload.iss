; Compile-time guard: the packaged binary must actually BE the version this installer claims to ship.
;
; Included by installer.iss and installer-arm64.iss AFTER each defines its own PublishDir.
;
; Why this exists: publishing writes to bin\<Platform>\Release\...\publish, and forgetting
; -p:Platform=x64 (or simply not publishing the other architecture) leaves a STALE binary sitting in that
; folder from an earlier release. ISCC then packages it happily and the resulting setup is stamped with
; MyAppVersion while carrying old code. Measured during the audit: the ARM64 installer compiled clean,
; reported itself as 4.99.14, and contained a 4.98.0.0 binary from a week earlier — sixteen versions
; stale, with no warning anywhere. A user would install "4.99.14", get 4.98.0 behaviour, and every version
; readout (About page, Add/Remove Programs) would agree with the wrong number.
;
; AGENTS.md documents the CI form of this trap; this makes the local form impossible rather than
; documented. The check costs nothing and fails the build with an actionable message.

#define PayloadExe PublishDir + "\UnifiedMessenger.exe"

#if !FileExists(PayloadExe)
  #error Installer payload is missing. Publish first, e.g. dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true
#endif

; GetFileVersion returns four parts ("4.99.14.0"); MyAppVersion is three ("4.99.14").
#define PayloadVersion GetFileVersion(PayloadExe)
#define ExpectedVersion MyAppVersion + ".0"

#if PayloadVersion != ExpectedVersion
  #pragma message "PAYLOAD VERSION MISMATCH"
  #pragma message "  installer claims : " + ExpectedVersion
  #pragma message "  payload actually : " + PayloadVersion
  #pragma message "  payload path     : " + PayloadExe
  #error Packaged binary does not match MyAppVersion. Re-publish this architecture before building the installer (the -p:Platform switch must match the PublishDir above).
#endif
