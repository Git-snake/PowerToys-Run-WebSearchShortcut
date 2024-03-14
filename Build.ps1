$ErrorActionPreference = "Stop"

[xml]$xml = Get-Content -Path "$PSScriptRoot\Directory.Build.Props"
$version = $xml.Project.PropertyGroup.Version

foreach ($platform in "ARM64", "x64")
{
    if (Test-Path -Path "$PSScriptRoot\Community.PowerToys.Run.Plugin.WebSearchShortcut\bin")
    {
        Remove-Item -Path "$PSScriptRoot\Community.PowerToys.Run.Plugin.WebSearchShortcut\bin\*" -Recurse
    }

    dotnet build $PSScriptRoot\PowerToys-Run-WebSearchShortcut.sln -c Release /p:Platform=$platform

    Remove-Item -Path "$PSScriptRoot\Community.PowerToys.Run.Plugin.WebSearchShortcut\bin\*" -Recurse -Include *.xml, *.pdb, PowerToys.*, Wox.*
    Rename-Item -Path "$PSScriptRoot\Community.PowerToys.Run.Plugin.WebSearchShortcut\bin\$platform\Release" -NewName "WebSearchShortcut"

    Compress-Archive -Path "$PSScriptRoot\Community.PowerToys.Run.Plugin.WebSearchShortcut\bin\$platform\WebSearchShortcut" -DestinationPath "$PSScriptRoot\WebSearchShortcut-$version-$platform.zip"
}
