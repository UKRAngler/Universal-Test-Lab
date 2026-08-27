param(
  [string]$MissionsRoot = ".\_mission_extract\mis.vromfs.bin_u\gamedata\missions",
  [string]$OutputPath = ".\data\combined_maps.tsv"
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$MissionsRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot $MissionsRoot))
$OutputPath = [IO.Path]::GetFullPath((Join-Path $scriptRoot $OutputPath))
if (-not (Test-Path -LiteralPath $MissionsRoot -PathType Container)) {
  throw "The extracted mis.vromfs missions folder was not found: $MissionsRoot"
}
$documentCache = @{}
$areaCache = @{}
$objectGroupCache = @{}

function Clean-Field([string]$value) {
  if ($null -eq $value) { return "" }
  return ($value -replace "[\t\r\n]", " " -replace "\s+", " ").Trim()
}

function Friendly-MapName([string]$id) {
  $name = $id -replace '^aaa_', '' -replace '^night_', ''
  $variant = ''
  if ($name -match '_02$') { $name = $name.Substring(0, $name.Length - 3); $variant = ' (Variant 2)' }
  $words = ($name -replace '_', ' ').Split(' ') | Where-Object { $_ }
  $title = ($words | ForEach-Object {
    if ($_.Length -le 1) { $_.ToUpperInvariant() }
    else { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1).ToLowerInvariant() }
  }) -join ' '
  if ($id -match '^night_') { $title += ' (Night)' }
  return $title + $variant
}

function Get-NamedBlocks([string]$text, [string]$name) {
  $results = New-Object System.Collections.Generic.List[object]
  $matches = [regex]::Matches($text, "(?m)^\s*" + [regex]::Escape($name) + "\s*\{")
  foreach ($match in $matches) {
    $open = $text.IndexOf('{', $match.Index)
    if ($open -lt 0) { continue }
    $depth = 0; $quoted = $false; $escaped = $false
    for ($i = $open; $i -lt $text.Length; $i++) {
      $c = $text[$i]
      if ($quoted) {
        if ($escaped) { $escaped = $false; continue }
        if ($c -eq '\') { $escaped = $true; continue }
        if ($c -eq '"') { $quoted = $false }
        continue
      }
      if ($c -eq '"') { $quoted = $true; continue }
      if ($c -eq '{') { $depth++ }
      elseif ($c -eq '}') {
        $depth--
        if ($depth -eq 0) {
          $results.Add([pscustomobject]@{ Start=$match.Index; Open=$open; End=$i; Text=$text.Substring($match.Index, $i-$match.Index+1) })
          break
        }
      }
    }
  }
  return $results
}

function Get-DirectChildBlocks([string]$containerText) {
  $results = New-Object System.Collections.Generic.List[object]
  $open = $containerText.IndexOf('{'); $end = $containerText.LastIndexOf('}')
  if ($open -lt 0 -or $end -le $open) { return $results }
  $cursor = $open + 1
  while ($cursor -lt $end) {
    $remaining = $containerText.Substring($cursor, $end - $cursor)
    $match = [regex]::Match($remaining, '(?m)^\s*"?([A-Za-z0-9_.@:$-]+)"?\s*\{')
    if (-not $match.Success) { break }
    $start = $cursor + $match.Index; $childOpen = $containerText.IndexOf('{', $start)
    $depth = 0; $quoted = $false; $escaped = $false; $childEnd = -1
    for ($i = $childOpen; $i -lt $end; $i++) {
      $c = $containerText[$i]
      if ($quoted) {
        if ($escaped) { $escaped = $false; continue }
        if ($c -eq '\') { $escaped = $true; continue }
        if ($c -eq '"') { $quoted = $false }
        continue
      }
      if ($c -eq '"') { $quoted = $true; continue }
      if ($c -eq '{') { $depth++ }
      elseif ($c -eq '}') { $depth--; if ($depth -eq 0) { $childEnd = $i; break } }
    }
    if ($childEnd -lt 0) { break }
    $results.Add([pscustomobject]@{ Name=$match.Groups[1].Value; Text=$containerText.Substring($start, $childEnd-$start+1) })
    $cursor = $childEnd + 1
  }
  return $results
}

function Resolve-Import([string]$resource) {
  $clean = ($resource -replace '\\', '/').TrimStart('/')
  $clean = $clean -replace '^(?i)gamedata/missions/', ''
  return [IO.Path]::GetFullPath((Join-Path $MissionsRoot ($clean -replace '/', '\')))
}

function Read-Document([string]$path) {
  $full = [IO.Path]::GetFullPath($path)
  if ($documentCache.ContainsKey($full)) { return $documentCache[$full] }
  if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $null }
  $document = [pscustomobject]@{ Path=$full; Text=[IO.File]::ReadAllText($full) }
  $documentCache[$full] = $document
  return $document
}

function Get-ImportDocuments([string]$entryPath) {
  $queue = New-Object System.Collections.Generic.Queue[string]
  $seen = @{}
  $result = New-Object System.Collections.Generic.List[object]
  $queue.Enqueue([IO.Path]::GetFullPath($entryPath))
  while ($queue.Count -gt 0) {
    $path = $queue.Dequeue()
    if ($seen.ContainsKey($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $seen[$path] = $true
    $document = Read-Document $path
    if ($null -eq $document) { continue }
    $text = $document.Text
    $result.Add($document)
    foreach ($imports in (Get-NamedBlocks $text 'imports' | Select-Object -First 1)) {
      foreach ($record in (Get-NamedBlocks $imports.Text 'import_record')) {
        $difficulty = [regex]::Match($record.Text, '(?m)^\s*difficulty:t\s*=\s*"([^"]+)"')
        if ($difficulty.Success -and $difficulty.Groups[1].Value -ne 'realistic') { continue }
        $file = [regex]::Match($record.Text, '(?m)^\s*file:t\s*=\s*"([^"]+)"')
        if ($file.Success) { $queue.Enqueue((Resolve-Import $file.Groups[1].Value)) }
      }
    }
  }
  return $result
}

function Get-DocumentAreas($document) {
  if ($areaCache.ContainsKey($document.Path)) { return $areaCache[$document.Path] }
  $areas = New-Object System.Collections.Generic.List[object]
  foreach ($container in (Get-NamedBlocks $document.Text 'areas' | Select-Object -First 1)) {
    foreach ($block in (Get-DirectChildBlocks $container.Text)) {
      $areas.Add([pscustomobject]@{ Name=$block.Name; Transform=(Get-Transform $block.Text) })
    }
  }
  $areaCache[$document.Path] = $areas
  return $areas
}

function Get-DocumentObjectGroups($document) {
  if ($objectGroupCache.ContainsKey($document.Path)) { return $objectGroupCache[$document.Path] }
  $objects = New-Object System.Collections.Generic.List[object]
  foreach ($units in (Get-NamedBlocks $document.Text 'units' | Select-Object -First 1)) {
    foreach ($block in (Get-DirectChildBlocks $units.Text)) {
      if ($block.Name -ne 'objectGroups') { continue }
      $objects.Add([pscustomobject]@{ Name=(Get-Field $block.Text 'name'); Transform=(Get-Transform $block.Text); Class=(Get-Field $block.Text 'unit_class') })
    }
  }
  $objectGroupCache[$document.Path] = $objects
  return $objects
}

function Get-Field([string]$text, [string]$name) {
  $match = [regex]::Match($text, '(?m)^\s*' + [regex]::Escape($name) + ':[A-Za-z0-9]+\s*=\s*"([^"]*)"')
  if ($match.Success) { return $match.Groups[1].Value }
  return ''
}

function Get-Transform([string]$text) {
  $match = [regex]::Match($text, '(?m)^\s*tm:m\s*=\s*(\[\[[^\r\n]+\]\])')
  if ($match.Success) { return Clean-Field $match.Groups[1].Value }
  return ''
}

function Find-Area($documents, [string[]]$names, [string]$preferredPath = '') {
  $candidates = New-Object System.Collections.Generic.List[object]
  foreach ($document in $documents) {
    foreach ($block in (Get-DocumentAreas $document)) {
      $index = -1
      for ($nameIndex = 0; $nameIndex -lt $names.Count; $nameIndex++) {
        if ($names[$nameIndex] -ieq $block.Name) { $index = $nameIndex; break }
      }
      if ($index -lt 0) { continue }
      $preference = $index * 100
      if ($preferredPath -and $document.Path -match $preferredPath) { $preference -= 50 }
      $candidates.Add([pscustomobject]@{ Score=$preference; Name=$block.Name; Transform=$block.Transform; Path=$document.Path })
    }
  }
  return $candidates | Where-Object { $_.Transform } | Sort-Object Score,Path | Select-Object -First 1
}

function Get-BriefingCaptureTargets([string]$rootText) {
  $sets = New-Object System.Collections.Generic.List[object]
  foreach ($briefing in (Get-NamedBlocks $rootText 'briefing' | Select-Object -First 1)) {
    foreach ($slide in (Get-NamedBlocks $briefing.Text 'slide')) {
      $difficulty = Get-Field $slide.Text 'difficulty'
      $priority = if ($difficulty -eq 'realistic') { 0 } elseif ($difficulty -eq 'hardcore') { 1 } elseif ($difficulty -eq 'arcade') { 2 } else { 3 }
      $targets = New-Object System.Collections.Generic.List[object]
      foreach ($icon in (Get-NamedBlocks $slide.Text 'icon')) {
        $iconType = Get-Field $icon.Text 'icontype'
        $target = Get-Field $icon.Text 'target'
        if ($iconType -notmatch '^(?i)basezone_([A-Z])$' -or -not $target) { continue }
        $targets.Add([pscustomobject]@{ Label=$Matches[1].ToUpperInvariant(); Target=$target })
      }
      if ($targets.Count -gt 0) { $sets.Add([pscustomobject]@{ Priority=$priority; Targets=$targets }) }
    }
  }
  $selected = $sets | Sort-Object Priority | Select-Object -First 1
  if ($selected) { return $selected.Targets }
  return @()
}

function Find-ObjectGroup($documents, [string]$name, [string]$preferredPath = '') {
  $candidates = New-Object System.Collections.Generic.List[object]
  foreach ($document in $documents) {
    foreach ($block in (Get-DocumentObjectGroups $document)) {
      if ($block.Name -ne $name) { continue }
      $score = if ($preferredPath -and $document.Path -match $preferredPath) { 0 } else { 10 }
      $candidates.Add([pscustomobject]@{ Score=$score; Transform=$block.Transform; Class=$block.Class; Path=$document.Path })
    }
  }
  return $candidates | Where-Object { $_.Transform -and $_.Class } | Sort-Object Score,Path | Select-Object -First 1
}

$rows = New-Object System.Collections.Generic.List[string]
$tankRoot = Join-Path $MissionsRoot 'cta\tanks'
$accepted = 0; $skipped = 0
foreach ($mission in (Get-ChildItem -LiteralPath $tankRoot -Recurse -File -Filter '*_dom.blk' | Where-Object { $_.FullName -notmatch '\\mainareas\\' } | Sort-Object FullName)) {
  $rootText = (Read-Document $mission.FullName).Text
  $missionBlock = Get-NamedBlocks $rootText 'mission' | Select-Object -First 1
  $level = if ($missionBlock) { Get-Field $missionBlock.Text 'level' } else { '' }
  if (-not $level) { $skipped++; continue }
  $documents = Get-ImportDocuments $mission.FullName
  $mapId = [IO.Path]::GetFileNameWithoutExtension($mission.Name) -replace '_dom$', ''
  $display = Friendly-MapName $mapId

  $spawns = New-Object System.Collections.Generic.List[object]
  foreach ($side in 1,2) {
    $g1 = Find-Area $documents @("t${side}_killarea_block01", "briefing_dom_t${side}_spawn_01_hardcore", "briefing_dom_t${side}_spawn_01") 'dom_battlearea_realistic'
    $g2 = Find-Area $documents @("t${side}_killarea_block02", "briefing_dom_t${side}_spawn_02_hardcore", "briefing_dom_t${side}_spawn_02") 'dom_battlearea_realistic'
    $air = Find-Area $documents @("t${side}_air_spawn_hardcore", "t${side}_air_spawn_arcade", "t${side}_air_spawn_top")
    $airfield = Find-ObjectGroup $documents "t${side}_airfield" 'mid_ranks'
    $heliNear = Find-ObjectGroup $documents "t${side}_helipad"
    $heliFar = Find-ObjectGroup $documents "t${side}_helipad_farspawn"
    if ($g1) { $spawns.Add([pscustomobject]@{ Kind='ground'; Side=$side; Option='ground_1'; Label='Ground spawn 1'; Transform=$g1.Transform; Class='' }) }
    if ($g2) { $spawns.Add([pscustomobject]@{ Kind='ground'; Side=$side; Option='ground_2'; Label='Ground spawn 2'; Transform=$g2.Transform; Class='' }) }
    if ($airfield) { $spawns.Add([pscustomobject]@{ Kind='aircraft'; Side=$side; Option='airfield'; Label='Airfield'; Transform=$airfield.Transform; Class=$airfield.Class }) }
    if ($air) { $spawns.Add([pscustomobject]@{ Kind='aircraft'; Side=$side; Option='air'; Label='Air spawn'; Transform=$air.Transform; Class='' }) }
    if ($heliNear) { $spawns.Add([pscustomobject]@{ Kind='helicopter'; Side=$side; Option='heli_near'; Label='Helipad — near'; Transform=$heliNear.Transform; Class=$heliNear.Class }) }
    if ($heliFar) { $spawns.Add([pscustomobject]@{ Kind='helicopter'; Side=$side; Option='heli_far'; Label='Helipad — far'; Transform=$heliFar.Transform; Class=$heliFar.Class }) }
  }

  $complete = $true
  foreach ($side in 1,2) {
    foreach ($required in 'ground_1','ground_2','airfield','air','heli_near','heli_far') {
      if (-not ($spawns | Where-Object { $_.Side -eq $side -and $_.Option -eq $required })) { $complete = $false }
    }
  }
  if (-not $complete) { $skipped++; continue }

  foreach ($spawn in $spawns) {
    $rows.Add((@($mapId,$display,$level,$spawn.Kind,$spawn.Side,$spawn.Option,$spawn.Label,$spawn.Transform,$spawn.Class) | ForEach-Object { Clean-Field ([string]$_) }) -join "`t")
  }
  foreach ($captureTarget in (Get-BriefingCaptureTargets $rootText)) {
    $capture = Find-Area $documents @($captureTarget.Target)
    if (-not $capture) { continue }
    $rows.Add((@($mapId,$display,$level,'capture',0,('capture_' + $captureTarget.Label.ToLowerInvariant()),$captureTarget.Label,$capture.Transform,'') | ForEach-Object { Clean-Field ([string]$_) }) -join "`t")
  }
  $accepted++
}

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $directory | Out-Null
[IO.File]::WriteAllLines($OutputPath, $rows, [Text.UTF8Encoding]::new($false))
Write-Output "Combined maps=$accepted Spawn rows=$($rows.Count) Skipped incomplete=$skipped Output=$OutputPath"
