# ------------------------------------------------------------------
#  Unity-3D巡检 · 启动器
#  用法：
#    start-all.cmd              拉起后端与前端并打开浏览器
#    start-backend.cmd          只启动 Go 后端
#    start-frontend.cmd         只启动 React 开发服务器
#    start-unity.cmd            构建（如有需要）并运行独立包
#    start-unity.cmd editor     打开 Unity 编辑器
#    start-unity.cmd build      强制重新构建并运行
#    start-all.cmd check        检查依赖、端口、场景与运行包
# ------------------------------------------------------------------

param(
    [string]$Action = "all"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$BackendDir = Join-Path $Root "backend"
$FrontendDir = Join-Path $Root "frontend"
$UnityDir = Join-Path $Root "unity"
$DataFile = Join-Path $BackendDir "data\issues.json"
$ProjectVersionFile = Join-Path $UnityDir "ProjectSettings\ProjectVersion.txt"
$ScenePath = Join-Path $UnityDir "Assets\Scenes\Main.unity"
$BuildDir = Join-Path $UnityDir "Build"
$ExePath = Join-Path $BuildDir "UnityInspection.exe"
$BuildLog = Join-Path $Root "Logs\build.log"
$BuildMethod = "Unity3DInspection.EditorTools.InspectionBuild.BuildFromCommandLine"
$ScriptPath = $PSCommandPath

$BackendPort = 8080
$FrontendPort = 5173

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
    Write-Host "    start-all.cmd             拉起后端与前端并打开浏览器"
    Write-Host "    start-backend.cmd         只启动 Go 后端（8080）"
    Write-Host "    start-frontend.cmd        只启动 React 开发服务器（5173）"
    Write-Host "    start-unity.cmd           构建（如有需要）并运行独立包"
    Write-Host "    start-unity.cmd editor    打开 Unity 编辑器"
    Write-Host "    start-unity.cmd build     强制重新构建并运行"
    Write-Host "    start-all.cmd check       检查依赖、端口、场景与运行包"
    Write-Host ""
}

function Get-ProjectVersion {
    if (-not (Test-Path -LiteralPath $ProjectVersionFile)) { return $null }
    $match = Select-String -Path $ProjectVersionFile -Pattern "m_EditorVersion:\s*(\S+)" | Select-Object -First 1
    if ($match -and $match.Matches.Count -gt 0) {
        return $match.Matches[0].Groups[1].Value.Trim()
    }
    return $null
}

function Resolve-Go {
    $candidates = New-Object System.Collections.ArrayList
    if ($env:GO_HOME) { [void]$candidates.Add((Join-Path $env:GO_HOME "bin\go.exe")) }
    if ($env:GOROOT) { [void]$candidates.Add((Join-Path $env:GOROOT "bin\go.exe")) }
    [void]$candidates.Add("D:\ctf\go\bin\go.exe")
    [void]$candidates.Add("C:\Program Files\Go\bin\go.exe")
    [void]$candidates.Add("C:\Go\bin\go.exe")

    $command = Get-Command go -ErrorAction SilentlyContinue
    if ($command) { [void]$candidates.Add($command.Source) }

    foreach ($path in $candidates) {
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }
    return $null
}

function Resolve-Node {
    $node = Get-Command node.exe -ErrorAction SilentlyContinue
    if (-not $node) { $node = Get-Command node -ErrorAction SilentlyContinue }

    # 优先 npm.cmd：npm.ps1 依赖 $MyInvocation.Statement 反解参数，
    # 在脚本里用 & $var 调用时会错位成「Unknown command」。
    $npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if (-not $npm) { $npm = Get-Command npm -ErrorAction SilentlyContinue }

    if ($node -and $npm) {
        return [pscustomobject]@{ Node = $node.Source; Npm = $npm.Source }
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
    $stamp = Join-Path $BuildDir ".build-stamp"
    if (Test-Path -LiteralPath $stamp) { return (Get-Item -LiteralPath $stamp).LastWriteTimeUtc }
    if (Test-Path -LiteralPath $ExePath) { return (Get-Item -LiteralPath $ExePath).LastWriteTimeUtc }
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

function Assert-Go {
    $go = Resolve-Go
    if (-not $go) {
        Write-Err "未找到 Go 工具链，后端无法启动。"
        Write-Hint "期望位置：D:\ctf\go\bin\go.exe"
        Write-Hint "或设置环境变量 GO_HOME 指向 Go 安装目录后重试。"
        Write-Hint "下载地址：https://go.dev/dl/"
        exit 2
    }
    return $go
}

function Assert-Node {
    $node = Resolve-Node
    if (-not $node) {
        Write-Err "未找到 Node.js，前端无法启动。"
        Write-Hint "需要 Node.js 20 或更高版本。"
        Write-Hint "下载地址：https://nodejs.org/"
        exit 2
    }
    return $node
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

function Start-BackendWindow {
    Write-Step "启动后端"
    if (Test-PortBusy $BackendPort) {
        if (Test-HttpOk "http://localhost:$BackendPort/api/health") {
            Write-Ok ("后端已在运行，跳过启动：http://localhost:{0}" -f $BackendPort)
        } else {
            Write-Warn ("端口 {0} 被占用且健康检查失败，跳过后端启动。" -f $BackendPort)
        }
        return
    }

    $go = Assert-Go
    Write-Ok ("Go: {0}" -f $go)
    Start-Process -FilePath "cmd.exe" -ArgumentList "/k", "powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" backend"
    Write-Ok ("后端窗口已打开：http://localhost:{0}" -f $BackendPort)
}

function Start-FrontendWindow {
    Write-Step "启动前端"
    if (Test-PortBusy $FrontendPort) {
        if (Test-HttpOk "http://localhost:$FrontendPort/") {
            Write-Ok ("前端已在运行，跳过启动：http://localhost:{0}" -f $FrontendPort)
        } else {
            Write-Warn ("端口 {0} 被占用且页面无法访问，跳过前端启动。" -f $FrontendPort)
        }
        return
    }

    $node = Assert-Node
    Write-Ok ("Node: {0}" -f $node.Node)
    Start-Process -FilePath "cmd.exe" -ArgumentList "/k", "powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" frontend"
    Write-Ok ("前端窗口已打开：http://localhost:{0}" -f $FrontendPort)
}

function Invoke-Backend {
    $go = Assert-Go
    Write-Ok ("Go: {0}" -f $go)

    $dataDir = Join-Path $BackendDir "data"
    if (-not (Test-Path -LiteralPath $dataDir)) {
        New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    }
    if (Test-PortBusy $BackendPort) {
        if (Test-HttpOk "http://localhost:$BackendPort/api/health") {
            Write-Ok ("后端已在运行：http://localhost:{0}/api/health" -f $BackendPort)
            Write-Hint ("需要重启时先结束占用该端口的进程：Get-NetTCPConnection -LocalPort {0} -State Listen" -f $BackendPort)
            exit 0
        }
        Write-Err ("端口 {0} 被占用，但健康检查失败。" -f $BackendPort)
        Write-Hint "占用该端口的可能不是本项目的后端，先结束该进程再重试。"
        Write-Hint ("查看占用：Get-NetTCPConnection -LocalPort {0} -State Listen | Select-Object OwningProcess" -f $BackendPort)
        exit 1
    }

    Write-Step "运行 go run ./cmd/server"
    Write-Hint ("数据文件：{0}" -f $DataFile)
    Write-Hint "按 Ctrl+C 停止服务。"
    Push-Location $BackendDir
    try {
        & $go run ./cmd/server
        $code = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($code -ne 0) {
        Write-Err ("后端退出，退出码 {0}" -f $code)
    }
    exit $code
}

function Invoke-Frontend {
    $node = Assert-Node
    Write-Ok ("Node: {0}" -f $node.Node)

    if (Test-PortBusy $FrontendPort) {
        if (Test-HttpOk "http://localhost:$FrontendPort/") {
            Write-Ok ("前端已在运行：http://localhost:{0}" -f $FrontendPort)
            Write-Hint ("需要重启时先结束占用该端口的进程：Get-NetTCPConnection -LocalPort {0} -State Listen" -f $FrontendPort)
            exit 0
        }
        Write-Err ("端口 {0} 被占用，但页面无法访问。" -f $FrontendPort)
        Write-Hint "占用该端口的可能不是本项目的开发服务器，先结束该进程再重试。"
        Write-Hint ("查看占用：Get-NetTCPConnection -LocalPort {0} -State Listen | Select-Object OwningProcess" -f $FrontendPort)
        exit 1
    }

    $npmPath = $node.Npm

    Push-Location $FrontendDir
    try {
        if (-not (Test-Path -LiteralPath (Join-Path $FrontendDir "node_modules"))) {
            Write-Step "安装前端依赖"
            & $npmPath install --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) {
                Write-Err "npm install 失败。"
                Write-Hint "检查网络或代理后，手动执行：cd frontend && npm install"
                exit 1
            }
            Write-Ok "依赖安装完成。"
        }

        Write-Step "运行 npm run dev"
        Write-Hint "/api 请求会转发到 http://localhost:8080"
        Write-Hint "按 Ctrl+C 停止服务。"
        & $npmPath run dev
        $code = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($code -ne 0) {
        Write-Err ("前端退出，退出码 {0}" -f $code)
    }
    exit $code
}

function Show-Check {
    Write-Step "环境检查"

    $go = Resolve-Go
    if ($go) {
        $goVersion = (& $go version) 2>$null
        Write-Ok ("Go        : {0}" -f $goVersion)
        Write-Hint ("路径      : {0}" -f $go)
    } else {
        Write-Err "Go        : 未找到（后端无法启动）"
        Write-Hint "期望位置：D:\ctf\go\bin\go.exe，或设置 GO_HOME"
    }

    $node = Resolve-Node
    if ($node) {
        $nodeVersion = (& $node.Node --version) 2>$null
        Write-Ok ("Node      : {0}" -f $nodeVersion)
        Write-Hint ("npm       : {0}（{1}）" -f (& $node.Npm --version), $node.Npm)
    } else {
        Write-Err "Node      : 未找到（前端无法启动）"
        Write-Hint "需要 Node.js 20+：https://nodejs.org/"
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

    if (Test-PortBusy $BackendPort) { Write-Warn ("端口 {0}   : 已被占用" -f $BackendPort) } else { Write-Ok ("端口 {0}   : 空闲" -f $BackendPort) }
    if (Test-PortBusy $FrontendPort) { Write-Warn ("端口 {0}   : 已被占用" -f $FrontendPort) } else { Write-Ok ("端口 {0}   : 空闲" -f $FrontendPort) }

    if (Test-Path -LiteralPath $DataFile) {
        $issues = Get-Content -LiteralPath $DataFile -Raw | ConvertFrom-Json
        Write-Ok ("数据文件  : 已存在（{0} 条）" -f @($issues).Count)
    } else {
        Write-Warn "数据文件  : 尚未创建，首次上报后生成"
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
    Write-Host ""
}

# ---------------------------------------------------------------- 主流程

$Action = $Action.ToLowerInvariant()

Write-Title

if ($Action -eq "check") {
    Show-Check
    exit 0
}

switch ($Action) {
    "backend" {
        Write-Host ("  目录: {0}" -f $BackendDir) -ForegroundColor Gray
        Invoke-Backend
    }
    "frontend" {
        Write-Host ("  目录: {0}" -f $FrontendDir) -ForegroundColor Gray
        Invoke-Frontend
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
    "editor" {
        Write-Host ("  工程: {0}" -f $UnityDir) -ForegroundColor Gray
        $selection = Assert-Unity
        Open-UnityEditor $selection (Get-ProjectVersion)
    }
    "all" {
        $go = Assert-Go
        $node = Assert-Node
        Write-Ok ("Go        : {0}" -f $go)
        Write-Ok ("Node      : {0}" -f $node.Node)

        Start-BackendWindow
        Start-Sleep -Seconds 3
        Start-FrontendWindow
        Start-Sleep -Seconds 4

        Write-Step "打开浏览器"
        Start-Process "http://localhost:$FrontendPort"
        Write-Ok ("后端：http://localhost:{0}/api/health" -f $BackendPort)
        Write-Ok ("前端：http://localhost:{0}" -f $FrontendPort)
        Write-Hint "关闭两个命令行窗口即可停止服务。"
        exit 0
    }
    default {
        Write-Err ("未知动作：{0}" -f $Action)
        Show-Usage
        exit 1
    }
}
