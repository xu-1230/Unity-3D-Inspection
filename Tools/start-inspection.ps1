# ------------------------------------------------------------------
#  Unity-3D巡检 · 启动器
#  用法：
#    start-docker.cmd           构建（如有需要）并启动 Web 两件套，打开浏览器
#    start-docker.cmd web       只启动 Web 两件套，不拉起 Unity 客户端
#    start-docker.cmd build     强制重建镜像后启动
#    start-docker.cmd down      停止容器（数据卷保留）
#    start-docker.cmd logs      跟踪容器日志
#    start-docker.cmd check     检查 Docker、端口、卷、场景与运行包
#    start-unity.cmd            构建（如有需要）并运行独立包
#    start-unity.cmd package    构建并打包成可分发的 release zip
#    start-unity.cmd editor     打开 Unity 编辑器
#    start-unity.cmd build      强制重新构建并运行
# ------------------------------------------------------------------

param(
    [string]$Action = "docker",
    [string]$Sub = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$UnityDir = Join-Path $Root "unity"
$EnvFile = Join-Path $Root ".env"
$ComposeFile = Join-Path $Root "compose.yaml"
$ProjectVersionFile = Join-Path $UnityDir "ProjectSettings\ProjectVersion.txt"
$ScenePath = Join-Path $UnityDir "Assets\Scenes\Main.unity"
$BuildDir = Join-Path $UnityDir "Build"
$ExePath = Join-Path $BuildDir "UnityInspection.exe"
$ReleaseDir = Join-Path $Root "release"
$ReleaseName = "UnityInspection-win64"
$BackendConfigName = "backend-config.json"
$HostDataFile = Join-Path $Root "backend\data\issues.db"
$BuildLog = Join-Path $Root "Logs\build.log"
$BuildMethod = "Unity3DInspection.EditorTools.InspectionBuild.BuildFromCommandLine"
$ScriptPath = $PSCommandPath

# 与 compose.yaml 中的默认端口保持一致，可用 .env 覆盖。
$BackendPort = 8080
$FrontendPort = 8088

function Write-Title {
    Write-Host ""
    Write-Host "  Unity-3D巡检 · 启动器" -ForegroundColor Cyan
    Write-Host "  --------------------------------------------------" -ForegroundColor DarkGray
}

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host ">> $Text" -ForegroundColor Cyan
}

function Write-Ok([string]$Text) {
    Write-Host "   [OK] $Text" -ForegroundColor Green
}

function Write-Warn([string]$Text) {
    Write-Host "   [!!] $Text" -ForegroundColor Yellow
}

function Write-Err([string]$Text) {
    Write-Host "   [XX] $Text" -ForegroundColor Red
}

function Write-Hint([string]$Text) {
    Write-Host "        $Text" -ForegroundColor Gray
}

function Show-Usage {
    Write-Host ""
    Write-Host "  用法：" -ForegroundColor White
    Write-Host "    start-docker.cmd           构建（如有需要）并启动 Web 两件套"
    Write-Host "    start-docker.cmd web       只启动 Web 两件套，不拉起客户端"
    Write-Host "    start-docker.cmd build     强制重建镜像后启动"
    Write-Host "    start-docker.cmd down      停止容器（数据卷保留）"
    Write-Host "    start-docker.cmd logs      跟踪容器日志"
    Write-Host "    start-docker.cmd check     检查 Docker、端口、卷与运行包"
    Write-Host "    start-unity.cmd            构建（如有需要）并运行独立包"
    Write-Host "    start-unity.cmd package    构建并打包 release zip"
    Write-Host "    start-unity.cmd editor     打开 Unity 编辑器"
    Write-Host "    start-unity.cmd build      强制重新构建并运行"
    Write-Host ""
}

function Get-EnvSetting([string]$Key, [string]$Default) {
    if (-not (Test-Path -LiteralPath $EnvFile)) { return $Default }
    $match = Select-String -LiteralPath $EnvFile -Pattern ("^\s*{0}\s*=\s*(.*)$" -f [regex]::Escape($Key)) | Select-Object -First 1
    if (-not $match -or $match.Matches.Count -eq 0) { return $Default }
    $value = $match.Matches[0].Groups[1].Value.Trim()
    if ($value -eq "") { return $Default }
    return $value
}

function Get-ProjectVersion {
    if (-not (Test-Path -LiteralPath $ProjectVersionFile)) { return $null }
    $match = Select-String -Path $ProjectVersionFile -Pattern "m_EditorVersion:\s*(\S+)" | Select-Object -First 1
    if ($match -and $match.Matches.Count -gt 0) {
        return $match.Matches[0].Groups[1].Value.Trim()
    }
    return $null
}

function Test-PortBusy([int]$Port) {
    try {
        $conn = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction Stop
        return ($null -ne $conn)
    } catch {
        $lines = netstat -ano | Select-String -Pattern (":{0}\s" -f $Port)
        return ($null -ne $lines -and $lines.Count -gt 0)
    }
}

function Test-HttpOk([string]$Url) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        return ($response.StatusCode -eq 200)
    } catch {
        return $false
    }
}

function Find-UnityEditors {
    $found = New-Object System.Collections.ArrayList
    $seen = @{}

    $add = {
        param([string]$Path)
        if ([string]::IsNullOrWhiteSpace($Path)) { return }
        if (-not (Test-Path -LiteralPath $Path)) { return }
        $full = (Resolve-Path -LiteralPath $Path).Path
        $key = $full.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { return }
        $seen[$key] = $true
        [void]$found.Add($full)
    }

    if ($env:UNITY_EXE) { & $add $env:UNITY_EXE }

    foreach ($registryRoot in @("HKLM:\SOFTWARE\Unity Technologies\Installer", "HKCU:\SOFTWARE\Unity Technologies\Installer")) {
        if (-not (Test-Path -LiteralPath $registryRoot)) { continue }
        try {
            Get-ChildItem -LiteralPath $registryRoot -ErrorAction Stop | ForEach-Object {
                $props = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
                if ($props -and $props."Location x64") { & $add (Join-Path $props."Location x64" "Editor\Unity.exe") }
            }
        } catch { }
    }

    $roots = New-Object System.Collections.ArrayList
    if ($env:ProgramFiles) { [void]$roots.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor")) }
    if (${env:ProgramFiles(x86)}) { [void]$roots.Add((Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor")) }
    [void]$roots.Add("D:\unityqq")
    [void]$roots.Add("D:\Unity")
    [void]$roots.Add("C:\Unity")

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            & $add (Join-Path $_.FullName "Editor\Unity.exe")
            & $add (Join-Path $_.FullName "Unity.exe")
        }
    }

    return $found
}

function Get-UnityVersionText([string]$Exe) {
    if (-not $Exe) { return "" }
    $product = (Get-Item -LiteralPath $Exe).VersionInfo.ProductVersion
    if (-not $product) { return "" }
    return $product.Split("_")[0]
}

function Select-UnityEditor([string]$ProjectVersion) {
    $editors = Find-UnityEditors
    if ($editors.Count -eq 0) { return $null }

    $exact = $null
    $newest = $null
    $newestVersion = New-Object System.Version -ArgumentList @(0, 0, 0)

    foreach ($exe in $editors) {
        $text = Get-UnityVersionText $exe
        if (-not $text) { continue }
        if ($ProjectVersion -and $text -eq $ProjectVersion) { $exact = $exe }
        if ($text -match "^(\d+)\.(\d+)\.(\d+)") {
            $version = New-Object System.Version -ArgumentList @([int]$matches[1], [int]$matches[2], [int]$matches[3])
            if ($version -gt $newestVersion) {
                $newestVersion = $version
                $newest = $exe
            }
        }
    }

    $chosen = $exact
    if (-not $chosen) { $chosen = $newest }
    if (-not $chosen) { return $null }

    return [pscustomobject]@{
        Path    = $chosen
        Version = Get-UnityVersionText $chosen
        Matched = [bool]$exact
        Editors = $editors
    }
}

function Test-WindowsBuildModule([string]$UnityExe) {
    if (-not $UnityExe) { return $false }
    $editorDir = Split-Path -Parent $UnityExe
    $moduleDir = Join-Path $editorDir "Data\PlaybackEngines\windowsstandalonesupport"
    return (Test-Path -LiteralPath $moduleDir)
}

function Get-BuildStamp {
    # 以 exe 的写入时间为准：构建戳只记录"脚本跑过一次构建"，
    # 一旦构建失败或产物没被覆盖，用它会误判成"已是最新"。
    if (Test-Path -LiteralPath $ExePath) { return (Get-Item -LiteralPath $ExePath).LastWriteTimeUtc }
    $stamp = Join-Path $BuildDir ".build-stamp"
    if (Test-Path -LiteralPath $stamp) { return (Get-Item -LiteralPath $stamp).LastWriteTimeUtc }
    return $null
}

function Test-BuildStale {
    if (-not (Test-Path -LiteralPath $ExePath)) { return $true }
    $reference = Get-BuildStamp
    if (-not $reference) { return $true }

    foreach ($dir in @((Join-Path $UnityDir "Assets"), (Join-Path $UnityDir "ProjectSettings"))) {
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        $newest = Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
                  Where-Object { $_.Extension -ne ".meta" } |
                  Sort-Object LastWriteTimeUtc -Descending |
                  Select-Object -First 1
        if ($newest -and $newest.LastWriteTimeUtc -gt $reference) { return $true }
    }
    return $false
}

function Assert-Docker {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        Write-Err "未找到 Docker，Web 两件套无法启动。"
        Write-Hint "安装 Docker Desktop：https://www.docker.com/products/docker-desktop/"
        exit 2
    }

    $composeVersion = (& docker compose version) 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "docker compose 子命令不可用。"
        Write-Hint "需要 Docker Desktop 自带的 Compose v2。"
        exit 2
    }
    Write-Ok ("Docker: {0}" -f ((& docker --version) 2>$null))
    Write-Ok ("Compose: {0}" -f $composeVersion)

    & docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Docker 引擎未运行。"
        Write-Hint "先启动 Docker Desktop，等状态变为 Running 后重试。"
        exit 1
    }
    return $docker.Source
}

function Assert-Unity {
    $projectVersion = Get-ProjectVersion
    $selection = Select-UnityEditor -ProjectVersion $projectVersion
    if (-not $selection) {
        Write-Err "未找到 Unity 编辑器，无法构建或打开工程。"
        Write-Hint "安装 Unity Hub 与 Unity 2022.3 LTS：https://unity.com/download"
        Write-Hint "或设置环境变量 UNITY_EXE 指向 Unity.exe 后重试。"
        exit 2
    }
    return $selection
}

function Invoke-Compose([string[]]$ComposeArgs) {
    # docker 把进度写到 stderr，而 $ErrorActionPreference = "Stop" 会把原生命令的
    # stderr 当成终止性错误，这里临时放宽并把两路输出都打印出来。
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    Push-Location $Root
    try {
        & docker compose @ComposeArgs | Out-Host
        return $LASTEXITCODE
    } finally {
        Pop-Location
        $ErrorActionPreference = $previous
    }
}

function Test-WebReady {
    return ((Test-HttpOk "http://localhost:$BackendPort/api/health") -and
            (Test-HttpOk "http://localhost:$FrontendPort/"))
}

function Wait-WebReady([int]$Seconds = 60) {
    Write-Step "等待服务就绪"
    Write-Hint ("前端 http://localhost:{0}，后端 http://localhost:{1}/api/health" -f $FrontendPort, $BackendPort)
    for ($i = 0; $i -lt $Seconds; $i++) {
        if (Test-WebReady) { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Start-UnityClient {
    if (-not (Test-Path -LiteralPath $ExePath)) {
        Write-Warn "尚未构建 Unity 运行包，跳过客户端启动。"
        Write-Hint "需要客户端时执行：start-unity.cmd"
        return
    }
    Write-Step "启动 Unity 客户端"
    Start-Process -FilePath $ExePath -WorkingDirectory $BuildDir
    Write-Ok "已启动，窗口标题为「Unity-3D巡检」"
}

function Invoke-DockerUp {
    param([switch]$SkipClient)

    $null = Assert-Docker

    Write-Step "启动 Web 两件套"
    if ((Invoke-Compose @("up", "-d")) -ne 0) {
        Write-Err "容器启动失败。"
        Write-Hint "查看详细日志：start-docker.cmd logs"
        exit 1
    }

    if (-not (Wait-WebReady)) {
        Write-Err "服务在 60 秒内未就绪。"
        Write-Hint "查看详细日志：start-docker.cmd logs"
        exit 1
    }
    Write-Ok ("前端：http://localhost:{0}" -f $FrontendPort)
    Write-Ok ("后端：http://localhost:{0}/api/health" -f $BackendPort)

    Write-Step "打开浏览器"
    Start-Process ("http://localhost:{0}" -f $FrontendPort)

    if (-not $SkipClient) { Start-UnityClient }
}

function Invoke-DockerBuild {
    $null = Assert-Docker

    Write-Step "重建镜像"
    Write-Hint "首次构建需要下载基础镜像与依赖，请勿关闭窗口。"
    if ((Invoke-Compose @("build")) -ne 0) {
        Write-Err "镜像构建失败。"
        Write-Hint "确认网络可访问基础镜像与依赖源后重试。"
        exit 1
    }
    Write-Ok "镜像已更新。"

    if ((Invoke-Compose @("up", "-d")) -ne 0) {
        Write-Err "容器启动失败。"
        exit 1
    }

    if (-not (Wait-WebReady)) {
        Write-Err "服务在 60 秒内未就绪。"
        Write-Hint "查看详细日志：start-docker.cmd logs"
        exit 1
    }
    Write-Ok ("前端：http://localhost:{0}" -f $FrontendPort)
    Write-Ok ("后端：http://localhost:{0}/api/health" -f $BackendPort)
}

function Invoke-DockerDown {
    $null = Assert-Docker

    Write-Step "停止容器"
    if ((Invoke-Compose @("down")) -ne 0) {
        Write-Err "停止失败。"
        exit 1
    }
    Write-Ok "已停止，数据卷 unity-3d-inspection-data 保留。"
    Write-Hint "需要连同数据一起清除时执行：docker compose down -v"
}

function Invoke-DockerLogs {
    $null = Assert-Docker

    Write-Step "跟踪容器日志"
    Write-Hint "按 Ctrl+C 退出。"
    $null = Invoke-Compose @("logs", "-f")
}

function Start-Exe {
    if (-not (Test-Path -LiteralPath $ExePath)) {
        Write-Err "运行包不存在：$ExePath"
        Write-Hint "先执行 start-unity.cmd build 构建一次。"
        exit 1
    }
    Write-Step "启动运行包"
    Start-Process -FilePath $ExePath -WorkingDirectory $BuildDir
    Write-Ok "已启动，窗口标题为「Unity-3D巡检」"
}

function Invoke-UnityBuild($Selection) {
    if (-not (Test-WindowsBuildModule $Selection.Path)) {
        Write-Err "缺少 Windows Build Support 模块，无法构建运行包。"
        Write-Hint "在 Unity Hub 中为该版本安装 Windows Build Support (Mono)。"
        Write-Hint "或改用 start-unity.cmd editor 打开编辑器按 Play。"
        return $false
    }

    $logDir = Split-Path -Parent $BuildLog
    if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Force -Path $logDir | Out-Null }
    if (Test-Path -LiteralPath $BuildLog) { Remove-Item -LiteralPath $BuildLog -Force }

    Write-Step "构建 Windows 运行包"
    Write-Hint ("编辑器：{0}" -f $Selection.Version)
    Write-Hint ("日志  ：{0}" -f $BuildLog)
    Write-Hint "首次构建需要数分钟，请勿关闭窗口。"

    $arguments = '-batchmode -quit -nographics -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $UnityDir, $BuildMethod, $BuildLog
    $process = Start-Process -FilePath $Selection.Path -ArgumentList $arguments -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $ExePath)) {
        Write-Err ("构建失败（退出码 {0}）" -f $process.ExitCode)
        if (Test-Path -LiteralPath $BuildLog) {
            Write-Hint "日志最后 20 行："
            Get-Content -LiteralPath $BuildLog -Tail 20 | ForEach-Object { Write-Host ("        " + $_) -ForegroundColor DarkGray }
        }
        return $false
    }

    New-Item -ItemType File -Path (Join-Path $BuildDir ".build-stamp") -Force | Out-Null
    Write-Ok ("构建完成：{0}" -f $ExePath)
    return $true
}

function Open-UnityEditor($Selection, [string]$ProjectVersion) {
    Write-Ok ("编辑器: {0}" -f $Selection.Version)
    Write-Hint ("路径  : {0}" -f $Selection.Path)
    if ($ProjectVersion -and -not $Selection.Matched) {
        Write-Warn ("工程版本 {0} 与编辑器 {1} 不一致，Unity 会提示升级，确认即可。" -f $ProjectVersion, $Selection.Version)
    }

    Write-Step "打开工程"
    Start-Process -FilePath $Selection.Path -ArgumentList ('-projectPath "{0}"' -f $UnityDir)
    Write-Ok "编辑器启动中。"
    Write-Hint "打开 Assets/Scenes/Main.unity 后按 Play。"
    exit 0
}

function Invoke-UnityRun([bool]$Force) {
    $projectVersion = Get-ProjectVersion
    $selection = Assert-Unity

    Write-Ok ("编辑器: {0}" -f $selection.Version)
    Write-Hint ("路径  : {0}" -f $selection.Path)
    if ($projectVersion -and -not $selection.Matched) {
        Write-Warn ("工程版本 {0} 与编辑器 {1} 不一致，Unity 首次打开时会提示升级，确认即可。" -f $projectVersion, $selection.Version)
    }

    if (-not $Force -and -not (Test-BuildStale)) {
        Write-Ok "运行包已是最新，直接启动。"
        Start-Exe
        exit 0
    }

    if (Invoke-UnityBuild $selection) {
        Start-Exe
        exit 0
    }

    Write-Warn "构建失败，改为打开编辑器，可手动按 Play 运行。"
    Open-UnityEditor $selection $projectVersion
}

function Invoke-UnityPackage {
    $projectVersion = Get-ProjectVersion
    $selection = Assert-Unity

    Write-Ok ("编辑器: {0}" -f $selection.Version)
    if ($projectVersion -and -not $selection.Matched) {
        Write-Warn ("工程版本 {0} 与编辑器 {1} 不一致，Unity 会提示升级，确认即可。" -f $projectVersion, $selection.Version)
    }

    if (-not (Invoke-UnityBuild $selection)) {
        Write-Err "构建失败，无法打包。"
        exit 1
    }

    Write-Step "汇总交付目录"
    $target = Join-Path $ReleaseDir $ReleaseName
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $BuildDir "*") -Destination $target -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $target ".build-stamp") -Force -ErrorAction SilentlyContinue

    # exe 启动时读取同目录的 backend-config.json，缺省指向本机后端。
    $configPath = Join-Path $target $BackendConfigName
    $configText = "{`r`n  `"baseUrl`": `"http://localhost:$BackendPort`"`r`n}`r`n"
    [System.IO.File]::WriteAllText($configPath, $configText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Ok ("已写入 {0}（baseUrl = http://localhost:{1}）" -f $BackendConfigName, $BackendPort)

    Write-Step "压缩为 zip"
    $zip = Join-Path $ReleaseDir ("{0}.zip" -f $ReleaseName)
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $target "*") -DestinationPath $zip -CompressionLevel Optimal
    if (-not (Test-Path -LiteralPath $zip)) {
        Write-Err "压缩失败。"
        exit 1
    }

    $sizeMb = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
    Write-Ok ("目录：{0}" -f $target)
    Write-Ok ("压缩包：{0}（{1} MB）" -f $zip, $sizeMb)
    Write-Hint "把 zip 解压到目标机器，按需修改 backend-config.json 里的后端地址。"
    exit 0
}

function Show-Check {
    Write-Step "环境检查"

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) {
        Write-Ok ("Docker    : {0}" -f ((& docker --version) 2>$null))
        $composeVersion = (& docker compose version) 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Ok ("Compose   : {0}" -f $composeVersion)
        } else {
            Write-Err "Compose   : 不可用（需要 Compose v2）"
        }

        & docker info *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "引擎      : 运行中"
        } else {
            Write-Err "引擎      : 未运行（先启动 Docker Desktop）"
        }

        foreach ($image in @("unity-3d-inspection/backend", "unity-3d-inspection/frontend")) {
            & docker image inspect ("{0}:latest" -f $image) *> $null
            if ($LASTEXITCODE -eq 0) {
                Write-Ok ("镜像      : {0}:latest 已存在" -f $image)
            } else {
                Write-Warn ("镜像      : {0}:latest 尚未构建" -f $image)
                Write-Hint "执行 start-docker.cmd build 构建。"
            }
        }

        & docker volume inspect "unity-3d-inspection-data" *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "数据卷    : unity-3d-inspection-data 已存在"
        } else {
            Write-Warn "数据卷    : 尚未创建，首次启动时自动创建"
        }
    } else {
        Write-Err "Docker    : 未找到（Web 两件套无法启动）"
        Write-Hint "安装 Docker Desktop：https://www.docker.com/products/docker-desktop/"
    }

    if (Test-PortBusy $BackendPort) {
        if (Test-HttpOk "http://localhost:$BackendPort/api/health") {
            Write-Ok ("端口 {0}   : 后端正在响应" -f $BackendPort)
        } else {
            Write-Warn ("端口 {0}   : 已被其它进程占用" -f $BackendPort)
        }
    } else {
        Write-Ok ("端口 {0}   : 空闲" -f $BackendPort)
    }

    if (Test-PortBusy $FrontendPort) {
        if (Test-HttpOk "http://localhost:$FrontendPort/") {
            Write-Ok ("端口 {0}   : 前端正在响应" -f $FrontendPort)
        } else {
            Write-Warn ("端口 {0}   : 已被其它进程占用" -f $FrontendPort)
        }
    } else {
        Write-Ok ("端口 {0}   : 空闲" -f $FrontendPort)
    }

    if (Test-Path -LiteralPath $HostDataFile) {
        $sizeKb = [math]::Round((Get-Item -LiteralPath $HostDataFile).Length / 1KB, 1)
        Write-Hint ("宿主机数据: backend\data\issues.db（{0} KB，仅本地 go run 使用）" -f $sizeKb)
    }

    $projectVersion = Get-ProjectVersion
    $selection = Select-UnityEditor -ProjectVersion $projectVersion
    if ($selection) {
        $matchText = "否（会提示升级）"
        if ($selection.Matched) { $matchText = "是" }
        Write-Ok ("Unity     : {0}" -f $selection.Version)
        Write-Hint ("路径      : {0}" -f $selection.Path)
        Write-Hint ("工程版本  : {0}，匹配：{1}" -f $(if ($projectVersion) { $projectVersion } else { "未声明" }), $matchText)

        if (Test-WindowsBuildModule $selection.Path) {
            Write-Ok "打包模块  : Windows Build Support 已安装"
        } else {
            Write-Warn "打包模块  : 缺少 Windows Build Support，无法构建运行包"
            Write-Hint "在 Unity Hub 中为该版本安装 Windows Build Support (Mono)"
        }
    } else {
        Write-Warn "Unity     : 未找到（客户端只能手工打开）"
        Write-Hint "可设置环境变量 UNITY_EXE 指向 Unity.exe"
    }

    if (Test-Path -LiteralPath $ScenePath) {
        Write-Ok "巡检场景  : 已生成"
    } else {
        Write-Warn "巡检场景  : 尚未生成（首次构建会自动生成）"
    }

    if (Test-Path -LiteralPath $ExePath) {
        if (Test-BuildStale) {
            Write-Warn ("运行包    : 已存在但已过期（{0}）" -f $ExePath)
        } else {
            Write-Ok ("运行包    : 已是最新（{0}）" -f $ExePath)
        }
    } else {
        Write-Warn "运行包    : 尚未构建（执行 start-unity.cmd）"
    }

    $zip = Join-Path $ReleaseDir ("{0}.zip" -f $ReleaseName)
    if (Test-Path -LiteralPath $zip) {
        Write-Ok ("交付包    : {0}" -f $zip)
    } else {
        Write-Warn "交付包    : 尚未生成（执行 start-unity.cmd package）"
    }

    if (-not (Test-Path -LiteralPath $ComposeFile)) {
        Write-Err ("编排文件  : 缺失 {0}" -f $ComposeFile)
    }
    Write-Host ""
}

# ---------------------------------------------------------------- 主流程

$Action = $Action.ToLowerInvariant()
$Sub = $Sub.ToLowerInvariant()

$BackendPort = [int](Get-EnvSetting "BACKEND_PORT" "8080")
$FrontendPort = [int](Get-EnvSetting "FRONTEND_PORT" "8088")

Write-Title

if ($Action -eq "check") {
    Show-Check
    exit 0
}

switch ($Action) {
    "docker" {
        Write-Host ("  编排: {0}" -f $ComposeFile) -ForegroundColor Gray
        switch ($Sub) {
            ""      { Invoke-DockerUp }
            "up"    { Invoke-DockerUp }
            "web"   { Invoke-DockerUp -SkipClient }
            "build" { Invoke-DockerBuild }
            "down"  { Invoke-DockerDown }
            "logs"  { Invoke-DockerLogs }
            "check" { Show-Check }
            default {
                Write-Err ("未知子命令：{0}" -f $Sub)
                Show-Usage
                exit 1
            }
        }
        exit 0
    }
    "run" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        Invoke-UnityRun $false
    }
    "unity" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        Invoke-UnityRun $false
    }
    "build" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        Invoke-UnityRun $true
    }
    "package" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        Invoke-UnityPackage
    }
    "editor" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        $selection = Assert-Unity
        Open-UnityEditor $selection (Get-ProjectVersion)
    }
    default {
        Write-Err ("未知动作：{0}" -f $Action)
        Show-Usage
        exit 1
    }
}
