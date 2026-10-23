# The shared memory bank: made at start-up, and makeable by name.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\shared_bank_test.ps1
#
# Nothing in either product made _shared, so a new user had to know it was a
# folder to create in File Explorer, and the rule that keeps bank names safe in
# a path strips a leading underscore: typing _shared into New memory bank made an
# ordinary bank called "shared", which is never sent as the shared one. Now the
# plugin makes it at start-up when it is missing, as Supervertaler for memoQ
# does. This pins that: it is made on a fresh data folder, an untouched one sends
# the model nothing, a bank that exists is left exactly as it is, a pending
# legacy-bank migration is not pre-empted, a failure is logged rather than
# thrown, nothing is made outside Studio, and New memory bank keeps the name.
#
# Everything happens under temp data folders, set in-process - the team folder
# too, so one configured on this computer is never touched. The translator's
# own banks are never read or written.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$PluginDll = Join-Path $root 'src\Supervertaler.Trados\bin\Studio18\Release\Supervertaler.Trados.dll'
if (-not (Test-Path $PluginDll)) { $PluginDll = Join-Path $root 'src\Supervertaler.Trados\bin\Studio19\Release\Supervertaler.Trados.dll' }
if (-not (Test-Path $PluginDll)) { throw "Build the plugin first: $PluginDll not found" }

# The plugin references Studio assemblies; resolve them from an install so the
# types load.
$studioDirs = @()
foreach ($pf in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
    foreach ($v in @('Studio18', 'Studio19')) {
        $d = Join-Path $pf "Trados\Trados Studio\$v"
        if (Test-Path $d) { $studioDirs += $d }
    }
}
$script:probed = @{}
[AppDomain]::CurrentDomain.add_AssemblyResolve([System.ResolveEventHandler] {
    param($s, $e)
    $name = ($e.Name -split ',')[0]
    if ($script:probed.ContainsKey($name)) { return $null }
    $script:probed[$name] = $true
    foreach ($dir in $studioDirs) {
        $c = Join-Path $dir "$name.dll"
        if (Test-Path $c) { try { return [Reflection.Assembly]::LoadFrom($c) } catch { return $null } }
    }
    return $null
})

$plugin = [Reflection.Assembly]::LoadFrom($PluginDll)
$Static = [Reflection.BindingFlags]'Public,NonPublic,Static'

$udp     = $plugin.GetType('Supervertaler.Trados.Settings.UserDataPath')
$paths   = $plugin.GetType('Supervertaler.Core.SupervertalerPaths')
$readerT = $plugin.GetType('Supervertaler.Core.MemoryBankReader')
$banksT  = $plugin.GetType('Supervertaler.Core.MemoryBanks')

$fails = 0; $checks = 0
function Check($ok, $label) {
    $script:checks++
    if (-not $ok) { $script:fails++ }
    Write-Host "$(if ($ok) {'PASS'} else {'FAIL'}) $label"
}

function Method($type, $name, $count) {
    $m = @($type.GetMethods($Static) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $count })
    if ($m.Count -ne 1) { throw "$($m.Count) methods named $name take $count arguments" }
    return $m[0]
}

# Points the plugin at a temp data folder, team folder included: setting the
# content root directly means a team folder in this computer's config.json is
# never read, so never written.
function UseDataFolder($dataRoot) {
    $paths.GetMethod('Set', $Static).Invoke($null, @([string]$dataRoot)) | Out-Null
    $paths.GetField('_contentRoot', $Static).SetValue($null, [string]$dataRoot)
    $banks = $paths.GetProperty('MemoryBanksDir', $Static).GetValue($null)
    if ($banks -ne (Join-Path $dataRoot 'memory-banks')) { throw "Not pointed at the temp folder: $banks" }
    return $banks
}

# EnsureSharedMemoryBank(banksRoot, log), collecting what it logs.
$script:logged = @()
$log = [Action[string]] { param($line) $script:logged += $line }
function Ensure($banksRoot) {
    $script:logged = @()
    $argv = New-Object object[] 2
    $argv[0] = [string]$banksRoot; $argv[1] = $log
    (Method $udp 'EnsureSharedMemoryBank' 2).Invoke($null, $argv) | Out-Null
}

# TryCreateMemoryBank(raw, out name, out error): the outs come back in the array.
function Make($name) {
    $argv = New-Object object[] 3
    $argv[0] = [string]$name; $argv[1] = $null; $argv[2] = $null
    $ok = (Method $udp 'TryCreateMemoryBank' 3).Invoke($null, $argv)
    return @{ Ok = $ok; Name = $argv[1]; Error = $argv[2] }
}

function FolderName($raw) { return (Method $udp 'NewBankFolderName' 1).Invoke($null, @([string]$raw)) }
function Skeleton($file, $bank) { return (Method $banksT 'SkeletonBody' 2).Invoke($null, @([string]$file, [string]$bank)) }
function Text($path) { return [IO.File]::ReadAllText($path) }
function Utf8($path, $text) { [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false))) }

# A client bank's context and prompt section, as the plugin reads them for a
# translation request.
function Context($bankDir) {
    $reader = [Activator]::CreateInstance($readerT, @([string]$bankDir))
    return $reader.LoadContext('PROJ-001', $null, 'en', 'nl', 32000, $null, $true)
}
function Prompt($ctx) { return (Method $readerT 'FormatForPrompt' 1).Invoke($null, @($ctx)) }
function SendsNoShared($ctx) {
    return $null -ne $ctx -and $null -eq $ctx.SharedBriefText -and $null -eq $ctx.SharedTerminologyText `
        -and $null -eq $ctx.SharedStyleText -and $ctx.SharedExtraArticles.Count -eq 0
}

$base = Join-Path $env:TEMP ('sv-shared-bank-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $base | Out-Null

try {
    # ---- 1. made at start-up, on a data folder that has never had a bank -----
    $data1 = Join-Path $base 'fresh'
    $banks = UseDataFolder $data1
    Ensure $banks
    $sh = Join-Path $banks '_shared'
    Check ((Test-Path "$sh\brief.md") -and (Test-Path "$sh\terminology.md") -and (Test-Path "$sh\style.md")) `
        'a fresh data folder gets _shared, with a brief, a terminology table and a style guide'
    Check (Test-Path "$sh\reference\README.md") 'and a reference folder, like any bank made in Trados'
    Check (-not (Test-Path (Join-Path $banks 'shared'))) 'named with its underscore'
    Check ($script:logged.Count -eq 1 -and $script:logged[0] -like 'Made the shared memory bank at *' -and $script:logged[0] -notlike '*, but *') `
        "and says so in the log: $($script:logged -join ' | ')"

    $same = $true
    foreach ($f in @('brief.md', 'terminology.md', 'style.md')) {
        if ((Text "$sh\$f") -cne (Skeleton $f '_shared')) { $same = $false }
    }
    Check $same 'each file is exactly core''s skeleton for _shared, the text the reader recognises as untouched'

    $bytes = [IO.File]::ReadAllBytes("$sh\brief.md")
    Check (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) `
        'written without a byte order mark, which would reach the prompt as three characters'

    # ---- 2. an untouched shared bank sends nothing --------------------------
    # The reader leaves out a file that is still exactly core's skeleton, so
    # making the bank for everyone costs nobody a token. Wording of its own would
    # not be recognised, and every request would carry empty headings, presented
    # to the model as the translator's decisions.
    $r = Make 'Acme Legal'
    $acme = Join-Path $banks 'acme-legal'
    Utf8 "$acme\brief.md" "# Acme`r`n`r`nAlways 'patent application', never 'patent request'.`r`n"
    $ctx = Context $acme
    Check (SendsNoShared $ctx) 'a shared bank nobody has written in sends nothing, beside a client bank that does'
    $prompt = Prompt $ctx
    Check ($prompt -match 'patent application' -and $prompt -notmatch '_shared') `
        'and the prompt carries the client''s brief and no trace of _shared'

    # ---- 3. a bank that exists is the translator's ---------------------------
    $mine = "# House defaults`r`n`r`nMy own rules.`r`n"
    Utf8 "$sh\brief.md" $mine
    Remove-Item "$sh\style.md"
    Remove-Item -Recurse "$sh\reference"

    Ensure $banks
    Check ((Text "$sh\brief.md") -ceq $mine) 'starting again leaves a written brief alone'
    Check (-not (Test-Path "$sh\style.md")) 'a file the translator deleted stays deleted'
    Check (-not (Test-Path "$sh\reference")) 'and so does a deleted reference folder'
    Check ($script:logged.Count -eq 0) 'and it logs nothing, because it did nothing'

    $ctx = Context $acme
    Check ($null -ne $ctx -and $ctx.SharedBriefText -eq $mine.Trim()) `
        'once written in, the shared brief is loaded under the client bank beside it'

    # A file someone else wrote between the folder check and the write - memoQ,
    # or a colleague on the team folder - stands. The skeleton writer uses
    # CreateNew, never check-then-write.
    $race = Join-Path $banks 'race'
    New-Item -ItemType Directory -Path $race | Out-Null
    $theirs = "# Theirs`r`n`r`nWritten first.`r`n"
    Utf8 "$race\brief.md" $theirs
    $problem = (Method $udp 'WriteNewBankSkeleton' 2).Invoke($null, @([string]$race, '_shared'))
    Check ((Text "$race\brief.md") -ceq $theirs -and (Test-Path "$race\style.md") -and $null -eq $problem) `
        'a file written by someone else first is kept, the missing ones are added, and that is not a problem'

    # ---- 4. a failure is reported, never thrown ------------------------------
    $notAFolder = Join-Path $base 'not-a-folder'
    [IO.File]::WriteAllText($notAFolder, 'a file where the banks folder should be')
    $threw = $false
    try { Ensure $notAFolder } catch { $threw = $true }
    Check (-not $threw) 'a banks folder that cannot be written does not stop a start'
    Check ($script:logged.Count -eq 1 -and $script:logged[0] -like 'Could not make the shared memory bank*') `
        "and the log says why: $(($script:logged -join ' ') -replace '\s+', ' ')"

    # ---- 5. a pending legacy-bank migration is not pre-empted ----------------
    # Trados offers to move an old single-folder bank only while memory-banks\
    # does not exist. Making _shared would create it and end the offer for good,
    # leaving the old bank contributing nothing to any prompt.
    $data2 = Join-Path $base 'legacy'
    New-Item -ItemType Directory -Force -Path (Join-Path $data2 'memory-bank') | Out-Null
    Utf8 (Join-Path $data2 'memory-bank\notes.md') "# Old bank`r`n"
    $banks2 = UseDataFolder $data2
    $needs = $udp.GetProperty('NeedsLegacyBankMigration', $Static)
    Check ($needs.GetValue($null)) 'a data folder with an old single-folder bank is waiting to be migrated'
    Ensure $banks2
    Check (-not (Test-Path $banks2)) 'and start-up makes no memory-banks folder there'
    Check ($needs.GetValue($null) -and $script:logged.Count -eq 0) 'so the migration is still offered next time'

    Remove-Item -Recurse (Join-Path $data2 'memory-bank')
    Ensure $banks2
    Check (Test-Path (Join-Path $banks2 '_shared\brief.md')) 'once migrated, the next start makes _shared'

    # ---- 6. never outside Studio ---------------------------------------------
    # The start-up call, pointed at a temp data folder that has no _shared.
    $data3 = Join-Path $base 'harness'
    $banks3 = UseDataFolder $data3
    New-Item -ItemType Directory -Force -Path $banks3 | Out-Null
    Check ([Diagnostics.Process]::GetCurrentProcess().ProcessName -notlike 'SDLTradosStudio*') 'this run is not Studio'
    (Method $udp 'EnsureSharedMemoryBank' 0).Invoke($null, @()) | Out-Null
    Check (-not (Test-Path (Join-Path $banks3 '_shared'))) 'and outside Studio the start-up call makes nothing'

    # ---- 7. New memory bank keeps the name -----------------------------------
    $data4 = Join-Path $base 'new-bank'
    $banks4 = UseDataFolder $data4

    Check ((FolderName '_shared') -eq '_shared') "the dialog's preview shows _shared: '$(FolderName '_shared')'"
    $r = Make '_shared'
    Check ($r.Ok -and $r.Name -eq '_shared') "typing _shared makes _shared: got '$($r.Name)'"
    Check (-not (Test-Path (Join-Path $banks4 'shared'))) 'and no ordinary bank called "shared" appears beside it'
    Check ((Text (Join-Path $banks4 '_shared\brief.md')) -ceq (Skeleton 'brief.md' '_shared')) 'with the skeleton the reader recognises'

    Check ((FolderName '  _Shared ') -eq '_shared') "in any case and with spaces, it is the same bank: '$(FolderName '  _Shared ')'"
    $r = Make '  _Shared '
    Check ((-not $r.Ok) -and $r.Error -match 'shared memory bank' -and $r.Error -match 'no need') `
        "asking again says it already exists and needs no switching: $($r.Error -replace '\s+', ' ')"

    Check ((FolderName '_beta') -eq 'beta') "any other leading underscore is still stripped: '$(FolderName '_beta')'"

    # ---- 8. client banks are as they were ------------------------------------
    $r = Make 'Acme Patents'
    Check ($r.Ok -and $r.Name -eq 'acme-patents') "a client bank is named as before: '$($r.Name)'"
    Check ((Text (Join-Path $banks4 'acme-patents\brief.md')) -match 'Who this client is') 'with the client template from core'
    $r = Make 'Acme Patents'
    Check ((-not $r.Ok) -and $r.Error -match 'already exists at') 'and asking again names the folder that exists'
    $r = Make '___'
    Check ((-not $r.Ok) -and $r.Error -match 'must contain') 'a name with nothing usable is refused with a reason'

    Utf8 (Join-Path $banks4 'acme-patents\brief.md') "# Acme Patents`r`n`r`nUK spelling throughout.`r`n"
    Check (SendsNoShared (Context (Join-Path $banks4 'acme-patents'))) 'the _shared made by New memory bank sends nothing either'
}
finally {
    $paths.GetMethod('Reset', $Static).Invoke($null, @()) | Out-Null
    Remove-Item -Recurse -Force $base -ErrorAction SilentlyContinue
}

Write-Host
if ($fails -gt 0) { Write-Host "$fails of $checks check(s) FAILED"; exit 1 }
Write-Host "All $checks checks passed."
