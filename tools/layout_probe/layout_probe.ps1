# Layout probe for the plugin's WinForms dialogs.
#
# Loads the built assembly, constructs each dialog off-screen with realistic
# text, forces a real layout pass, then measures every control and reports:
#
#   * overlapping controls
#   * anything spilling outside the form
#   * fixed-size labels whose text needs more height than the label has
#
# Written after a survey dialog shipped with its question clipped mid-sentence,
# and then, after a first fix, with controls overlapping each other. Both were
# found from screenshots after the fact. This finds them before.
#
# Usage (Trados Studio does NOT need to be running):
#   pwsh -File tools/layout_probe/layout_probe.ps1
#   pwsh -File tools/layout_probe/layout_probe.ps1 -StudioVersion 19
#
# Exits 0 when every dialog passes, 1 otherwise, so it can gate a release.

param(
    [string]$StudioVersion = "18",
    [string]$Configuration = "Release"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dll = Join-Path $repoRoot "src\Supervertaler.Trados\bin\Studio$StudioVersion\$Configuration\Supervertaler.Trados.dll"

if (-not (Test-Path $dll)) {
    Write-Output "Assembly not found: $dll"
    Write-Output "Build first:  dotnet build src/Supervertaler.Trados/Supervertaler.Trados.csproj -c $Configuration -p:TradosStudioVersion=$StudioVersion"
    exit 1
}

$asm = [System.Reflection.Assembly]::LoadFrom($dll)

# Each case supplies the LONGEST realistic text, since that is what breaks a
# layout. Survey text is typed into the admin dashboard per question, so its
# length is genuinely unknown at build time - test accordingly.
$cases = @(
    @{
        Type = "Supervertaler.Trados.Controls.SurveyDialog"
        Name = "SurveyDialog (yes/no)"
        Args = @(
            "If SuperMemory (memory banks) disappeared from Supervertaler tomorrow, would you miss it?",
            "Yes, I'd miss it",
            "No, I wouldn't really notice",
            "yesno")
    },
    @{
        Type = "Supervertaler.Trados.Controls.SurveyDialog"
        Name = "SurveyDialog (open, long question)"
        Args = @(
            "Which parts of Supervertaler do you actually use day to day, and is there anything you expected to be there that isn't? Any detail helps, however small.",
            "Yes", "No", "open")
    },
    @{
        Type = "Supervertaler.Trados.Controls.AnnouncementDialog"
        Name = "AnnouncementDialog"
        Args = @(
            "A quick heads-up about SuperMemory.",
            "I've been testing whether SuperMemory's memory banks actually improve translations, and I want to share what I found - and what I'm considering building instead - before I change anything.",
            "https://github.com/orgs/Supervertaler/discussions/245",
            "Read the full post ->",
            "Got it")
    }
)

$failed = 0

function Get-Leaves($ctrl, $offX, $offY, $acc) {
    foreach ($c in $ctrl.Controls) {
        $absX = $offX + $c.Left
        $absY = $offY + $c.Top
        if ($c.Controls.Count -gt 0) {
            Get-Leaves $c $absX $absY $acc
        } else {
            $t = if ($c.Text -and $c.Text.Length -gt 42) { $c.Text.Substring(0, 42) + "..." } else { $c.Text }
            [void]$acc.Add([pscustomobject]@{
                Name = $c.GetType().Name; Text = $t; Ctrl = $c
                X = $absX; Y = $absY; W = $c.Width; H = $c.Height
                Bottom = $absY + $c.Height; Right = $absX + $c.Width
            })
        }
    }
}

function Test-Labels($ctrl, [ref]$issues) {
    foreach ($c in $ctrl.Controls) {
        if ($c.Controls.Count -gt 0) { Test-Labels $c $issues; continue }

        # Only FIXED-SIZE labels can clip: an AutoSize label grows to fit its
        # text (MaximumSize caps width but lets height grow). Measuring an
        # AutoSize label at its own resulting width would just re-wrap text it
        # already fitted on one line - a false positive.
        if (($c -is [System.Windows.Forms.Label]) -and $c.Text -and (-not $c.AutoSize)) {
            $needed = [System.Windows.Forms.TextRenderer]::MeasureText(
                $c.Text, $c.Font, (New-Object System.Drawing.Size($c.Width, 0)),
                [System.Windows.Forms.TextFormatFlags]::WordBreak)
            if ($needed.Height -gt $c.Height) {
                $t = if ($c.Text.Length -gt 42) { $c.Text.Substring(0, 42) + "..." } else { $c.Text }
                $issues.Value += "TEXT CLIPPED (needs $($needed.Height)px, has $($c.Height)px): '$t'"
            }
        }
    }
}

foreach ($case in $cases) {
    Write-Output ""
    Write-Output "=== $($case.Name) ==="

    $type = $asm.GetType($case.Type)
    if (-not $type) {
        Write-Output "  TYPE NOT FOUND: $($case.Type)"
        $failed++
        continue
    }

    $ctor = $type.GetConstructors(
        [System.Reflection.BindingFlags]::Instance -bor
        [System.Reflection.BindingFlags]::Public -bor
        [System.Reflection.BindingFlags]::NonPublic)[0]

    $dlg = $ctor.Invoke($case.Args)
    $dlg.StartPosition = 'Manual'
    $dlg.Location = New-Object System.Drawing.Point(-4000, -4000)
    $dlg.Show()
    $dlg.PerformLayout()
    [System.Windows.Forms.Application]::DoEvents()

    Write-Output ("  form: {0} x {1}" -f $dlg.ClientSize.Width, $dlg.ClientSize.Height)

    $leaves = New-Object System.Collections.ArrayList
    Get-Leaves $dlg 0 0 $leaves

    $issues = @()

    for ($i = 0; $i -lt $leaves.Count; $i++) {
        for ($j = $i + 1; $j -lt $leaves.Count; $j++) {
            $a = $leaves[$i]; $b = $leaves[$j]
            if (($a.X -lt $b.Right) -and ($b.X -lt $a.Right) -and
                ($a.Y -lt $b.Bottom) -and ($b.Y -lt $a.Bottom)) {
                $issues += "OVERLAP: '$($a.Text)' <-> '$($b.Text)'"
            }
        }
    }

    foreach ($l in $leaves) {
        if ($l.Bottom -gt $dlg.ClientSize.Height) {
            $issues += "PAST FORM BOTTOM by $($l.Bottom - $dlg.ClientSize.Height)px: '$($l.Text)'"
        }
        if ($l.Right -gt $dlg.ClientSize.Width) {
            $issues += "PAST FORM RIGHT by $($l.Right - $dlg.ClientSize.Width)px: '$($l.Text)'"
        }
    }

    Test-Labels $dlg ([ref]$issues)

    if ($issues.Count -eq 0) {
        Write-Output "  OK - $($leaves.Count) controls, no overlap, no clipping"
    } else {
        foreach ($i in $issues) { Write-Output "  $i" }
        $failed++
    }

    $dlg.Close()
    $dlg.Dispose()
}

Write-Output ""
if ($failed -eq 0) {
    Write-Output "All dialogs passed."
    exit 0
} else {
    Write-Output "$failed dialog(s) FAILED."
    exit 1
}
