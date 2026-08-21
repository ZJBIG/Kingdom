[CmdletBinding()]
param([string]$ProjectRoot=(Join-Path $PSScriptRoot '..\..'),[string]$OutputPath=(Join-Path $PSScriptRoot '..\..\data\content-dependency\content-dependency-analysis.md'))
$ErrorActionPreference='Stop'; $assetRoot=Join-Path $ProjectRoot 'Assets\Resources\Datas'; $guidToId=@{}; $defs=@{}
function LoadDefs([string]$kind){foreach($f in Get-ChildItem (Join-Path $assetRoot $kind) -Recurse -Filter '*.asset'){$t=Get-Content $f.FullName -Raw;$i=[regex]::Match($t,'(?m)^[ \t]*id:\s*(\S+)\s*$');$m=Get-Content ($f.FullName+'.meta') -Raw;$g=[regex]::Match($m,'(?m)^guid:\s*(\S+)\s*$');if($i.Success -and $g.Success){$script:guidToId[$g.Groups[1].Value]=$i.Groups[1].Value;$script:defs[$i.Groups[1].Value]=[pscustomobject]@{Id=$i.Groups[1].Value;Kind=$kind;Text=$t}}}}
LoadDefs 'Resource';LoadDefs 'Building';LoadDefs 'Research';LoadDefs 'Workshop'
function F([object]$d,[string]$n){$m=[regex]::Match($d.Text,"(?ms)^  ${n}:[ \t]*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:[ \t]*|\z)");if(!$m.Success){return @()};return @([regex]::Matches($m.Value,'guid:\s*([0-9a-f]+)')|%{$guidToId[$_.Groups[1].Value]}|?{$_})}
function P([object]$d,[string]$n){$m=[regex]::Match($d.Text,"(?ms)^  ${n}:[ \t]*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:[ \t]*|\z)");if(!$m.Success){return @()};return @([regex]::Matches($m.Value,'(?ms)- resource:.*?guid:\s*([0-9a-f]+).*?amount:\s*"([^"]+)"')|%{[pscustomobject]@{Guid=$_.Groups[1].Value;Amount=[double]::Parse($_.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture)}})}
function T([object]$d){$m=[regex]::Match($d.Text,'(?m)^[ \t]*TechLevel:\s*(\d+)');if($m.Success){return [int]$m.Groups[1].Value};return 0}
function A([object]$d){$m=[regex]::Match($d.Text,'(?m)^[ \t]*AdvancesTechLevel:\s*(\d+)');return $m.Success -and $m.Groups[1].Value -eq '1'}
function All([string[]]$ids,[hashtable]$set){foreach($id in $ids){if(!$set.ContainsKey($id)){return $false}};return $true}
$research=@($defs.Values|? { $_.Kind -eq 'Research' });$buildings=@($defs.Values|? { $_.Kind -eq 'Building' });$workshops=@($defs.Values|? { $_.Kind -eq 'Workshop' });$producers=@{}
foreach($b in $buildings){foreach($r in F $b resourceGenerationRates){if(!$producers.ContainsKey($r)){$producers[$r]=@()};$producers[$r]+=$b}}
$maxGeneration=@{}
foreach($b in $buildings){
    foreach($p in P $b resourceGenerationRates){
        if(!$maxGeneration.ContainsKey($p.Guid)-or $p.Amount-gt $maxGeneration[$p.Guid]){$maxGeneration[$p.Guid]=$p.Amount}
    }
}
$sectorViolations=@()
$sectorTerritoryViolations=@()
$sectorRoot=Join-Path $assetRoot 'Sector'
if(Test-Path $sectorRoot){
    foreach($f in Get-ChildItem $sectorRoot -Recurse -Filter '*.asset'){
        $t=Get-Content $f.FullName -Raw
        $id=([regex]::Match($t,'(?m)^[ \t]*id:\s*(\S+)\s*$')).Groups[1].Value
        $domain=([regex]::Match($t,'(?m)^  domain:\s*(\d+)\s*$')).Groups[1].Value
        $territoryText=([regex]::Match($t,'(?m)^  territoryReward:\s*"?([^"\r\n]+)"?\s*$')).Groups[1].Value
        $territory=0d
        if($territoryText){$territory=[double]::Parse($territoryText,[Globalization.CultureInfo]::InvariantCulture)}
        if($territory -le 0d -or ($domain -eq '1' -and $territory -lt 100000d)){
            $sectorTerritoryViolations += "$id territoryReward=$territory domain=$domain"
        }
        $m=[regex]::Match($t,'(?ms)^  occupiedResourceRatesPerSecond:[ \t]*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:[ \t]*|\z)')
        if(!$m.Success){continue}
        foreach($p in [regex]::Matches($m.Value,'(?ms)- resource:.*?guid:\s*([0-9a-f]+).*?amount:\s*"([^"]+)"')){
            $guid=$p.Groups[1].Value
            $amount=[double]::Parse($p.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture)
            $domestic=if($maxGeneration.ContainsKey($guid)){$maxGeneration[$guid]}else{0}
            if($domestic -le 0 -or $amount -gt ($domestic*0.2+1e-9)){
                $resourceId=if($guidToId.ContainsKey($guid)){$guidToId[$guid]}else{$guid}
                $sectorViolations += [pscustomobject]@{Sector=$id;Resource=$resourceId;Rate=$amount;DomesticMax=$domestic;Limit=$domestic*0.2}
            }
        }
    }
}
$sectorGuidToId=@{};$sectorTexts=@{}
if(Test-Path $sectorRoot){foreach($meta in Get-ChildItem $sectorRoot -Filter '*.meta' -File){$asset=$meta.FullName.Substring(0,$meta.FullName.Length-5);if(!(Test-Path -LiteralPath $asset -PathType Leaf)){continue};$mt=Get-Content $meta.FullName -Raw;$gm=[regex]::Match($mt,'(?m)^guid:\s*(\S+)\s*$');$st=Get-Content $asset -Raw;$im=[regex]::Match($st,'(?m)^  id:\s*(\S+)\s*$');if($gm.Success-and $im.Success){$sectorGuidToId[$gm.Groups[1].Value]=$im.Groups[1].Value;$sectorTexts[$im.Groups[1].Value]=$st}}}
$sectorProgressionViolations=@();$sectorPrerequisites=@{}
foreach($id in $sectorTexts.Keys){$m=[regex]::Match($sectorTexts[$id],'(?ms)^  prerequisiteSectors:\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)');$prerequisites=@();if($m.Success){foreach($p in [regex]::Matches($m.Groups[1].Value,'guid:\s*([0-9a-f]+)')){if($sectorGuidToId.ContainsKey($p.Groups[1].Value)){$prerequisites+=$sectorGuidToId[$p.Groups[1].Value]}}};$sectorPrerequisites[$id]=$prerequisites}
$expectedHome=@{Moon='LowOrbit';Mars='Moon';MainAsteroidBelt='Mars';JovianSystem='MainAsteroidBelt'}
foreach($pair in $expectedHome.GetEnumerator()){if(!$sectorPrerequisites.ContainsKey($pair.Key)-or @($sectorPrerequisites[$pair.Key]).Count-ne 1-or $sectorPrerequisites[$pair.Key][0] -ne $pair.Value){$sectorProgressionViolations+="$($pair.Key) must directly follow $($pair.Value)"}}
function ReachesMainAsteroid([string]$id,[hashtable]$edges,[hashtable]$vis){if($id -eq 'MainAsteroidBelt'){return $true};if($vis.ContainsKey($id)){return $false};$vis[$id]=$true;foreach($p in @($edges[$id])){if(ReachesMainAsteroid $p $edges $vis){return $true}};return $false}
foreach($id in $sectorTexts.Keys){$t=$sectorTexts[$id];$domain=([regex]::Match($t,'(?m)^  domain:\s*(\d+)\s*$')).Groups[1].Value;if($domain -eq '1' -and !(ReachesMainAsteroid $id $sectorPrerequisites @{})){$sectorProgressionViolations+="$id must be reachable from MainAsteroidBelt"}}
function N([string]$text,[string]$field){$pattern='(?m)^  '+[regex]::Escape($field)+':\s*"?([^"\r\n]+)"?\s*$';$m=[regex]::Match($text,$pattern);if($m.Success){return [double]::Parse($m.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)};return 0d}
$powerGeneration=0d;$powerConsumption=0d;$logisticsGeneration=0d;$logisticsConsumption=0d
foreach($b in $buildings){$powerGeneration+=N $b.Text 'powerProductionRate';$powerConsumption+=N $b.Text 'powerConsumptionRate';$logisticsGeneration+=N $b.Text 'logisticsProductionRate';$logisticsConsumption+=N $b.Text 'logisticsConsumptionRate'}
$flowViolations=@();if($powerConsumption-gt 0-and $powerGeneration-le 0){$flowViolations+='Power has consumers but no producer'};if($logisticsConsumption-gt 0-and $logisticsGeneration-le 0){$flowViolations+='Logistics has consumers but no producer'}
$campaignViolations=@();$advancedCampaignResources=@('TitaniumAlloy','Composite','PhantomAlloy','PhantomWeave','PhaseMaterial')
$sectorRoot=Join-Path $assetRoot 'Sector'
if(Test-Path $sectorRoot){
    foreach($f in Get-ChildItem $sectorRoot -Recurse -Filter '*.asset'){
        $t=Get-Content $f.FullName -Raw
        $id=([regex]::Match($t,'(?m)^  id:\s*(\S+)\s*$')).Groups[1].Value
        $domain=([regex]::Match($t,'(?m)^  domain:\s*(\d+)\s*$')).Groups[1].Value
        if($domain -ne '1'){continue}
        $campaignSection=[regex]::Match($t,'(?ms)^  campaignResourceRatesPerSecond:\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)').Groups[1].Value
        $hasCost=$false;$hasAdvanced=$false
        foreach($m in [regex]::Matches($campaignSection,'(?ms)- resource:.*?guid:\s*([0-9a-f]+).*?amount:\s*"([^"]+)"')){
            $resourceId=if($guidToId.ContainsKey($m.Groups[1].Value)){$guidToId[$m.Groups[1].Value]}else{$m.Groups[1].Value}
            $amount=[double]::Parse($m.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture)
            $hasCost=$hasCost-or $amount-gt 0;$hasAdvanced=$hasAdvanced-or ($advancedCampaignResources -contains $resourceId)
        }
        $food=N $t 'campaignFoodPerSecond';$mult=N $t 'campaignProgressMultiplier';$territory=N $t 'territoryReward'
        if($food -le 0 -or !$hasCost -or !$hasAdvanced -or $mult -le 0 -or $mult -gt (1d/60d) -or $territory -lt 100000){$campaignViolations+=$id}
    }
}
$sinks=@{}
foreach($d in @($research+$buildings+$workshops)){
    foreach($field in @('resourceRequirements','resourceConsumptionRates')){
        foreach($r in F $d $field){if(!$sinks.ContainsKey($r)){$sinks[$r]=@()};$sinks[$r]+=$d}
    }
}
$highTierResourceViolations=@();foreach($resourceId in @('TitaniumAlloy','Composite','PhantomAlloy','PhantomWeave','PhaseMaterial')){if(!$producers.ContainsKey($resourceId)-or @($producers[$resourceId]).Count-eq 0){$highTierResourceViolations+="$resourceId has no producer"};if(!$sinks.ContainsKey($resourceId)-or @($sinks[$resourceId]).Count-lt 2){$highTierResourceViolations+="$resourceId has fewer than two sinks"}}
$done=@{};$resources=@{WoodLog=$true};$built=@{};$purchased=@{};$tech=2
foreach($r in $research){if((T $r)-le 2){$done[$r.Id]=$true}}
foreach($b in $buildings){if((T $b)-le 2){$built[$b.Id]=$true;foreach($r in F $b resourceGenerationRates){$resources[$r]=$true}}}
$changed=$true
while($changed){$changed=$false;foreach($r in $research){if($done.ContainsKey($r.Id)){continue};if((T $r)-gt $tech -and -not((A $r)-and (T $r)-eq $tech+1)){continue};if(!(All (F $r prerequisites) $done)){continue};if(!(All (F $r resourceRequirements) $resources)){continue};$done[$r.Id]=$true;if((A $r)-and (T $r)-gt $tech){$tech=T $r};$changed=$true};foreach($w in $workshops){if(!$done.ContainsKey('IndustrialWorkshop')-or $purchased.ContainsKey($w.Id)-or (T $w)-gt $tech){continue};if(!(All (F $w requiredResearch) $done)){continue};if(!(All (F $w requiredWorkshopUpgrades) $purchased)){continue};if(!(All (F $w resourceRequirements) $resources)){continue};$purchased[$w.Id]=$true;$changed=$true};foreach($b in $buildings){if($built.ContainsKey($b.Id)-or (T $b)-gt $tech){continue};if(!(All (F $b requiredResearch) $done)){continue};if(!(All (F $b requiredWorkshopUpgrades) $purchased)){continue};if(!(All (F $b resourceRequirements) $resources)){continue};$built[$b.Id]=$true;foreach($r in F $b resourceGenerationRates){$resources[$r]=$true};$changed=$true}}
function Missing([string]$id){$x=@("Missing resource: $id");if(!$producers.ContainsKey($id)){return ($x+'Required producer: <none>')-join "`n"};foreach($b in $producers[$id]){$x+="Required producer: $($b.Id)";foreach($r in F $b requiredResearch){if(!$done.ContainsKey($r)){$x+="Producer blocked by: $r"}};foreach($c in F $b resourceRequirements){if(!$resources.ContainsKey($c)){$x+="Producer cost blocked by: $c"}}};return $x-join "`n"}
function Reason([object]$d,[string]$rf){foreach($r in F $d $rf){if(!$done.ContainsKey($r)){return "Missing prerequisite: $r"}};foreach($r in F $d resourceRequirements){if(!$resources.ContainsKey($r)){return Missing $r}};return "TechLevel condition: $(T $d)"}
$ur=@($research|?{!$done.ContainsKey($_.Id)}|%{[pscustomobject]@{Id=$_.Id;Reason=(Reason $_ prerequisites)}});$ub=@($buildings|?{!$built.ContainsKey($_.Id)}|%{[pscustomobject]@{Id=$_.Id;Reason=(Reason $_ requiredResearch)}})
$cycles=@();function Visit([object]$d,[hashtable]$vis,[hashtable]$seen,[Collections.ArrayList]$path){if($seen.ContainsKey($d.Id)){return};if($vis.ContainsKey($d.Id)){$s=$path.IndexOf($d.Id);if($s-lt 0){$s=0};$cycles+=(($path[$s..($path.Count-1)]+$d.Id)-join ' -> ');return};$vis[$d.Id]=$true;[void]$path.Add($d.Id);foreach($r in F $d prerequisites){if($defs.ContainsKey($r)){Visit $defs[$r] $vis $seen $path}};[void]$path.RemoveAt($path.Count-1);$vis.Remove($d.Id);$seen[$d.Id]=$true}
$vis=@{};$seen=@{};$path=[Collections.ArrayList]@();foreach($r in $research){Visit $r $vis $seen $path}
$dead=@();foreach($r in $research){foreach($x in F $r resourceRequirements){if($x -eq 'WoodLog'){continue};if($producers.ContainsKey($x)){$eligible=@($producers[$x]|?{(F $_ requiredResearch)-notcontains $r.Id});if($eligible.Count-eq 0){foreach($b in $producers[$x]){$dead+="$($r.Id) -> requires $x -> producer $($b.Id) requires $($r.Id)"}}}}};foreach($b in $buildings){foreach($x in F $b resourceRequirements){if($x -eq 'WoodLog'){continue};if($producers.ContainsKey($x)-and $producers[$x].Count-eq 1-and $producers[$x][0].Id-eq $b.Id){$dead+="$($b.Id) -> construction requires $x -> produced only by $($b.Id)"}}}
$o=[Text.StringBuilder]::new();[void]$o.AppendLine('# Content dependency analysis');[void]$o.AppendLine('================================');[void]$o.AppendLine('UNREACHABLE RESEARCH');[void]$o.AppendLine('================================');if(!$ur.Count){[void]$o.AppendLine('None')}else{foreach($x in $ur){[void]$o.AppendLine("`nResearch: $($x.Id)`nBlocked reason:`n$($x.Reason)")}};[void]$o.AppendLine("`n================================`nUNREACHABLE BUILDINGS`n================================");if(!$ub.Count){[void]$o.AppendLine('None')}else{foreach($x in $ub){[void]$o.AppendLine("`nBuilding: $($x.Id)`nBlocked reason:`n$($x.Reason)")}};[void]$o.AppendLine("`n================================`nRESOURCE SOURCE/SINK AUDIT`n================================");foreach($resource in @($defs.Values|?{$_.Kind -eq 'Resource'}|Sort-Object Id)){[void]$o.AppendLine("$($resource.Id): sources=$(@($producers[$resource.Id]).Count); sinks=$(@($sinks[$resource.Id]).Count)")};[void]$o.AppendLine("`n================================`nRESOURCE DEADLOCKS`n================================");if(!$dead.Count){[void]$o.AppendLine('None')}else{$dead|%{[void]$o.AppendLine($_)}};[void]$o.AppendLine("`n================================`nRESEARCH CYCLES`n================================");if(!$cycles.Count){[void]$o.AppendLine('None')}else{$cycles|%{[void]$o.AppendLine($_)}}
$sectorSummary=if(!$sectorViolations.Count){'None'}else{(($sectorViolations|ForEach-Object{"$($_.Sector): $($_.Resource) rate=$($_.Rate) domesticMax=$($_.DomesticMax) limit=$($_.Limit)"})-join "`n")};[void]$o.AppendLine("`n================================`nSECTOR PRODUCTION LIMIT (20% OF DOMESTIC MAX)`n================================`n$sectorSummary")
[void]$o.AppendLine("`n================================`nPOWER / LOGISTICS FLOW AUDIT (ONE COPY PER BUILDING)`n================================`nPower: production=$powerGeneration; consumption=$powerConsumption; net=$($powerGeneration-$powerConsumption)`nLogistics: production=$logisticsGeneration; consumption=$logisticsConsumption; net=$($logisticsGeneration-$logisticsConsumption)`nViolations: $(if($flowViolations.Count){$flowViolations -join '; '}else{'None'})")
[void]$o.AppendLine("`n================================`nINTERSTELLAR CAMPAIGN AUDIT`n================================`nViolations: $(if($campaignViolations.Count){$campaignViolations -join ', '}else{'None'})")
[void]$o.AppendLine("`n================================`nHIGH-TIER RESOURCE SOURCE/SINK AUDIT`n================================`nViolations: $(if($highTierResourceViolations.Count){$highTierResourceViolations -join '; '}else{'None'})")
[void]$o.AppendLine("`n================================`nSOLAR SYSTEM SECTOR PROGRESSION AUDIT`n================================`nViolations: $(if($sectorProgressionViolations.Count){$sectorProgressionViolations -join '; '}else{'None'})")
[void]$o.AppendLine("`n================================`nSECTOR TERRITORY REWARD AUDIT`n================================`nViolations: $(if($sectorTerritoryViolations.Count){$sectorTerritoryViolations -join '; '}else{'None'})")
$rs=if(!$ur.Count){'ALL'}else{'NO'};$bs=if(!$ub.Count){'ALL'}else{'NO'};$ds=if(!$dead.Count){'None'}else{'FOUND'};$cs=if(!$cycles.Count){'None'}else{'FOUND'};[void]$o.AppendLine("`nSummary`nDefinitions: $($defs.Count)`nResearch definitions: $($research.Count)`nBuilding definitions: $($buildings.Count)`nResearch reachable: $rs`nBuilding reachable: $bs`nResource deadlock: $ds`nResearch cycle: $cs`nHighest TechLevel: $tech")
$rs=if(!$ur.Count){'ALL'}else{'NO'};$bs=if(!$ub.Count){'ALL'}else{'NO'};$ds=if(!$dead.Count){'None'}else{'FOUND'};$cs=if(!$cycles.Count){'None'}else{'FOUND'};[void]$o.AppendLine("`n================================`nREPAIR SUGGESTIONS`n================================");if(!$dead.Count){[void]$o.AppendLine('None')}else{foreach($d in $dead){[void]$o.AppendLine("`nProblem:`n$d`n`nPossible fixes:`nOption A: Remove the blocking resource from the research or construction cost.`nOption B: Change the producer requiredResearch or constructionCost.`nOption C: Add an alternative producer reachable before the blocked node.")}};New-Item (Split-Path $OutputPath) -ItemType Directory -Force|Out-Null;[IO.File]::WriteAllText($OutputPath,$o.ToString(),[Text.UTF8Encoding]::new($false));$o.ToString()
if($sectorViolations.Count){throw "Sector production limit regression: $($sectorViolations.Count) violation(s)."};if($flowViolations.Count){throw "Power/logistics flow regression: $($flowViolations.Count) violation(s)."};if($campaignViolations.Count){throw "Interstellar campaign regression: $($campaignViolations.Count) violation(s)."};if($highTierResourceViolations.Count){throw "High-tier resource source/sink regression: $($highTierResourceViolations.Count) violation(s)."};if($sectorProgressionViolations.Count){throw "Solar System sector progression regression: $($sectorProgressionViolations.Count) violation(s)."};if($sectorTerritoryViolations.Count){throw "Sector territory reward regression: $($sectorTerritoryViolations.Count) violation(s)."}
