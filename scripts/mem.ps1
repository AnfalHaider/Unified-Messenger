#Requires -Version 5.1
<#
.SYNOPSIS
  Dependency-free memory helpers for Unified Messenger agents.

.DESCRIPTION
  index   - regenerate docs/memory/INDEX.md from lesson frontmatter
  report  - regenerate docs/memory/LAST_REPORT.md from agent logs
  mem     - print live lessons whose triggers match a topic (retrieval)
  check   - context budget, lesson frontmatter, supersedes targets, stale file references,
            and that this script is still ASCII-only

  Usage - always pass -ExecutionPolicy Bypass:
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 index
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 report
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 mem "publish platform"
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 check

  Why -ExecutionPolicy Bypass: every execution-policy scope on the owner's machine is Undefined,
  which means Restricted, and powershell.exe then refuses to load any script at all ("running
  scripts is disabled on this system"). The argument is process-scoped and changes no setting.
  Some agent hosts set a policy for you; a plain shell does not, so the command without it
  worked for one agent and failed for another.

  Why ASCII-only: Windows PowerShell 5.1 decodes a script that has no byte-order mark in the
  ANSI code page, so any non-ASCII literal in this file is corrupted in everything it writes.
  `check` fails if one appears. Output files are written as UTF-8 without a BOM.

  Correcting a lesson: lessons are immutable. Add a NEW lesson carrying
  `supersedes: [old-id]`. `mem` stops returning the old one and INDEX.md marks it superseded.
#>
[CmdletBinding()]
param(
  [Parameter(Position = 0)]
  [ValidateSet('index', 'report', 'mem', 'check')]
  [string]$Command = 'check',

  [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
  [string[]]$TopicParts
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$MemoryRoot = Join-Path $RepoRoot 'docs\memory'
$LessonsDir = Join-Path $MemoryRoot 'lessons'
$IndexPath = Join-Path $MemoryRoot 'INDEX.md'
$ReportPath = Join-Path $MemoryRoot 'LAST_REPORT.md'
$MemoryMd = Join-Path $MemoryRoot 'MEMORY.md'

# Always-loaded instruction files (Phase 1 baseline + memory index budget).
$AlwaysLoaded = @(
  (Join-Path $RepoRoot 'AGENTS.md'),
  (Join-Path $RepoRoot 'CLAUDE.md'),
  (Join-Path $RepoRoot '.cursorrules')
)
# Soft ceiling: fail check if always-loaded files exceed this (chars).
# Baseline was 57392; allow ~20% headroom for the absorbed operating-rules block.
$BudgetCeilingChars = 70000

# Fields whose absence makes a lesson malformed. `evidence` is deliberately not here: the first
# ten lessons predate the field and lessons are immutable, so a hard failure on it would make
# `check` fail forever on files nobody may edit. Its absence is reported as a warning instead.
$RequiredFields = @('id', 'date', 'agent', 'title', 'triggers', 'files', 'cost')

function Read-Utf8([string]$Path) {
  return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8).TrimStart([char]0xFEFF)
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
  [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-Frontmatter {
  param([string]$Path)
  $raw = Read-Utf8 $Path
  if ($raw -notmatch '(?s)^---\r?\n(.*?)\r?\n---\r?\n') {
    return $null
  }
  $block = $Matches[1]
  $map = @{}
  $currentList = $null
  foreach ($line in ($block -split "`r?`n")) {
    if ($line -match '^\s*([A-Za-z0-9_]+):\s*(.*)$') {
      $key = $Matches[1]
      $val = $Matches[2].Trim()
      if ($val -eq '' -or $val -eq '[]') {
        $map[$key] = @()
        $currentList = $key
      }
      elseif ($val -match '^\[(.*)\]$') {
        $inner = $Matches[1].Trim()
        if ($inner -eq '') { $map[$key] = @() }
        else {
          $map[$key] = @($inner.Split(',') | ForEach-Object {
              $_.Trim().Trim('"').Trim("'")
            } | Where-Object { $_ -ne '' })
        }
        $currentList = $null
      }
      else {
        $map[$key] = $val.Trim('"').Trim("'")
        $currentList = $null
      }
    }
    elseif ($currentList -and $line -match '^\s*-\s+(.+)$') {
      if ($map[$currentList] -isnot [System.Array]) { $map[$currentList] = @() }
      $map[$currentList] += $Matches[1].Trim().Trim('"').Trim("'")
    }
  }
  $map['_block'] = $block
  $map['_path'] = $Path
  $map['_file'] = Split-Path $Path -Leaf
  return $map
}

function Get-Lessons {
  if (-not (Test-Path $LessonsDir)) { return @() }
  Get-ChildItem -LiteralPath $LessonsDir -Filter '*.md' -File |
    ForEach-Object { Get-Frontmatter $_.FullName } |
    Where-Object { $_ -ne $null }
}

# id -> id of the lesson that replaces it. A new lesson's `supersedes` list is the supported way
# to correct an immutable one; a `superseded_by` on the old file is honoured for compatibility.
function Get-SupersededMap([object[]]$Lessons) {
  $map = @{}
  foreach ($l in $Lessons) {
    if ($l.superseded_by) { $map["$($l.id)"] = "$($l.superseded_by)" }
    foreach ($old in @($l.supersedes)) {
      if (-not [string]::IsNullOrWhiteSpace($old)) { $map["$old"] = "$($l.id)" }
    }
  }
  return $map
}

function Invoke-Index {
  $lessons = @(Get-Lessons | Sort-Object { $_.id })
  $superseded = Get-SupersededMap $lessons
  $lines = @(
    '<!-- GENERATED by scripts/mem.ps1 index - do not hand-edit -->',
    '',
    '# Memory lesson index',
    '',
    "| id | date | agent | status | title | triggers |",
    "|---|---|---|---|---|---|"
  )
  foreach ($l in $lessons) {
    $triggers = if ($l.triggers) { ($l.triggers -join ', ') } else { '' }
    $id = "$($l.id)"
    $status = if ($superseded.ContainsKey($id)) { "superseded by $($superseded[$id])" } elseif ($l.status) { $l.status } else { 'live' }
    $lines += "| $id | $($l.date) | $($l.agent) | $status | $($l.title) | $triggers |"
  }
  $lines += ''
  $lines += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  Write-Utf8NoBom $IndexPath (($lines -join "`n") + "`n")
  Write-Output "Wrote $IndexPath ($($lessons.Count) lessons)"
}

function Get-AgentLogDirs {
  Get-ChildItem -LiteralPath $MemoryRoot -Directory |
    Where-Object { $_.Name -notin @('lessons') -and (Test-Path (Join-Path $_.FullName 'log.md')) }
}

function Invoke-Report {
  $lines = @(
    '<!-- GENERATED by scripts/mem.ps1 report - do not hand-edit -->',
    '',
    '# Last report (newest log entry per agent)',
    ''
  )
  foreach ($dir in @(Get-AgentLogDirs | Sort-Object Name)) {
    $logPath = Join-Path $dir.FullName 'log.md'
    $raw = Read-Utf8 $logPath
    $entry = ''
    $m = [regex]::Match($raw, '(?ms)^##\s+.*?(?=^##\s+|\z)')
    if ($m.Success) { $entry = ([string]$m.Value).Trim() }
    $lines += "## $($dir.Name)"
    $lines += ''
    if ($entry) { $lines += $entry } else { $lines += '_No entries yet._' }
    $lines += ''
    $lines += '---'
    $lines += ''
  }
  $lines += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  Write-Utf8NoBom $ReportPath (($lines -join "`n") + "`n")
  Write-Output "Wrote $ReportPath"
}

function Invoke-Mem {
  $topic = ($TopicParts -join ' ').Trim()
  if ([string]::IsNullOrWhiteSpace($topic)) {
    Write-Error "Usage: mem.ps1 mem <topic>"
    exit 2
  }
  $tokens = @($topic.ToLowerInvariant() -split '\s+' | Where-Object { $_.Length -gt 1 })
  $lessons = @(Get-Lessons)
  $superseded = Get-SupersededMap $lessons
  $hits = @()
  foreach ($l in $lessons) {
    if ($superseded.ContainsKey("$($l.id)")) { continue }
    if ($l.status -and $l.status -ne 'live') { continue }
    $trig = @($l.triggers | ForEach-Object { "$_".ToLowerInvariant() })
    $hay = ($trig + @("$($l.title)".ToLowerInvariant()) + @("$($l.id)".ToLowerInvariant())) -join ' '
    $score = 0
    foreach ($t in $tokens) {
      if ($hay -like "*$t*") { $score++ }
    }
    if ($score -gt 0) {
      $hits += [pscustomobject]@{ Score = $score; Lesson = $l }
    }
  }
  if ($hits.Count -eq 0) {
    Write-Output "No live lessons matched topic: $topic"
    return
  }
  $hits | Sort-Object Score -Descending | ForEach-Object {
    $l = $_.Lesson
    Write-Output ("---- {0} (score {1}) ----" -f $l.id, $_.Score)
    Write-Output ("title: {0}" -f $l.title)
    Write-Output ("cost:  {0}" -f $l.cost)
    Write-Output ("file:  docs/memory/lessons/{0}" -f $l._file)
    Write-Output ("triggers: {0}" -f ($l.triggers -join ', '))
    Write-Output ''
  }
}

function Get-CharCount([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { return 0 }
  return (Read-Utf8 $Path).Length
}

function Invoke-Check {
  $failed = $false
  $total = 0
  Write-Output '== context budget =='
  foreach ($f in $AlwaysLoaded) {
    $c = Get-CharCount $f
    $total += $c
    $rel = $f.Substring($RepoRoot.Length).TrimStart('\')
    Write-Output ("  {0}: {1} chars (~{2} tok)" -f $rel, $c, [math]::Round($c / 3.6))
  }
  if (Test-Path $MemoryMd) {
    $mc = Get-CharCount $MemoryMd
    Write-Output ("  docs/memory/MEMORY.md (on-demand index): {0} chars (~{1} tok)" -f $mc, [math]::Round($mc / 3.6))
  }
  Write-Output ("  ALWAYS_LOADED_TOTAL: {0} chars (~{1} tok)" -f $total, [math]::Round($total / 3.6))
  Write-Output ("  CEILING: {0} chars" -f $BudgetCeilingChars)
  if ($total -gt $BudgetCeilingChars) {
    Write-Output '  FAIL: always-loaded budget exceeded'
    $failed = $true
  }
  else {
    Write-Output '  PASS: under ceiling'
  }

  Write-Output ''
  Write-Output '== lesson frontmatter =='
  $lessonFiles = @()
  if (Test-Path $LessonsDir) { $lessonFiles = @(Get-ChildItem -LiteralPath $LessonsDir -Filter '*.md' -File) }
  $lessons = @()
  $seen = @{}
  $malformed = 0
  $noEvidence = @()
  foreach ($file in $lessonFiles) {
    $l = Get-Frontmatter $file.FullName
    if ($null -eq $l) {
      Write-Output ("  FAIL {0}: no frontmatter block" -f $file.Name)
      $malformed++
      continue
    }
    $lessons += $l
    $errs = @()
    foreach ($k in $RequiredFields) {
      if (-not $l.ContainsKey($k)) { $errs += "missing $k" }
    }
    if (-not $l.ContainsKey('status') -and -not $l.ContainsKey('superseded_by')) { $errs += 'missing status' }
    if ($l.ContainsKey('triggers') -and @($l.triggers).Count -eq 0) { $errs += 'triggers is empty' }
    $base = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    if ($l.id -and "$($l.id)" -ne $base) { $errs += "id '$($l.id)' does not match the filename" }
    if ($l.date -and "$($l.date)" -notmatch '^\d{4}-\d{2}-\d{2}$') { $errs += "date '$($l.date)' is not YYYY-MM-DD" }
    if ($l._block -match '(?m)^files:.*["'']') { $errs += 'files entries must be unquoted' }
    if ($l.id) {
      if ($seen.ContainsKey("$($l.id)")) { $errs += "duplicate id, also in $($seen["$($l.id)"])" }
      else { $seen["$($l.id)"] = $file.Name }
    }
    if ($errs.Count -gt 0) {
      Write-Output ("  FAIL {0}: {1}" -f $file.Name, ($errs -join '; '))
      $malformed++
    }
    elseif (-not $l.ContainsKey('evidence')) {
      $noEvidence += "$($l.id)"
    }
  }
  if ($malformed -gt 0) { $failed = $true }
  else { Write-Output ("  PASS: {0} lesson(s) well-formed" -f $lessons.Count) }
  if ($noEvidence.Count -gt 0) {
    Write-Output ("  WARN: {0} lesson(s) have no evidence field (immutable; supersede to add it): {1}" -f $noEvidence.Count, ($noEvidence -join ', '))
  }

  Write-Output ''
  Write-Output '== supersedes targets =='
  $badRefs = 0
  foreach ($l in $lessons) {
    foreach ($old in (@($l.supersedes) + @($l.superseded_by))) {
      if ([string]::IsNullOrWhiteSpace($old)) { continue }
      if (-not $seen.ContainsKey("$old")) {
        Write-Output ("  FAIL {0} -> no lesson with id '{1}'" -f $l.id, $old)
        $badRefs++
      }
    }
  }
  if ($badRefs -gt 0) { $failed = $true } else { Write-Output '  PASS: every supersedes target exists' }

  Write-Output ''
  Write-Output '== stale file references =='
  $staleCount = 0
  foreach ($l in $lessons) {
    foreach ($ref in @($l.files)) {
      if ([string]::IsNullOrWhiteSpace($ref)) { continue }
      $candidate = if ([System.IO.Path]::IsPathRooted($ref)) { $ref } else { Join-Path $RepoRoot $ref }
      if (-not (Test-Path -LiteralPath $candidate)) {
        Write-Output ("  STALE {0} -> {1}" -f $l.id, $ref)
        $staleCount++
        $failed = $true
      }
    }
  }
  if ($staleCount -eq 0) { Write-Output '  PASS: no missing files named in lessons' }

  Write-Output ''
  Write-Output '== script encoding =='
  $nonAscii = 0
  foreach ($b in [System.IO.File]::ReadAllBytes($PSCommandPath)) {
    if ($b -gt 127) { $nonAscii++ }
  }
  if ($nonAscii -gt 0) {
    Write-Output ("  FAIL: scripts/mem.ps1 contains {0} non-ASCII byte(s); PowerShell 5.1 will corrupt them" -f $nonAscii)
    $failed = $true
  }
  else {
    Write-Output '  PASS: ASCII-only'
  }

  Write-Output ''
  if ($failed) {
    Write-Output 'CHECK FAILED'
    exit 1
  }
  Write-Output 'CHECK OK'
  exit 0
}

switch ($Command) {
  'index' { Invoke-Index }
  'report' { Invoke-Report }
  'mem' { Invoke-Mem }
  'check' { Invoke-Check }
}
