$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$projectPath = Split-Path $PSScriptRoot -Parent
$dialogue = Get-Content -LiteralPath (Join-Path $projectPath 'Assets/Stillworks/Dialogue.json') -Raw | ConvertFrom-Json
$destination = Join-Path $projectPath 'Assets/Stillworks/Audio/Narration'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$narrator = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $narrator.SelectVoice('Microsoft David Desktop')
    $narrator.Rate = 0
    foreach ($cue in $dialogue.cues) {
        $narrator.SetOutputToWaveFile((Join-Path $destination ($cue.id + '.wav')))
        $narrator.Speak($cue.text)
        $narrator.SetOutputToNull()
    }
} finally { $narrator.Dispose() }
Write-Output ('Created ' + $dialogue.cues.Count + ' original narrator clips.')
