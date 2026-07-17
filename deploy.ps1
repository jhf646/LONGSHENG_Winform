<#============================================================================
  上海氢晨库存管理系统 - 一键部署脚本
  功能：SQL数据库初始化 + 后端API服务部署 + Web前端发布
  用法：以管理员身份运行 PowerShell，执行本脚本
============================================================================#>

param(
    [string]$ApiPort = "5000",
    [string]$SqlServer = "PC-20260524GEOP\SQLEXPRESS",
    [string]$SqlUser = "sa",
    [string]$SqlPassword = "123456",
    [string]$DatabaseName = "LongShenStorage",
    [string]$DeployPath = "C:\LongShenStorage",
    [switch]$SkipDb,
    [switch]$SkipApi,
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"

# 路径修正：脚本所在目录包含 LongShenStorageApi/LongShenStorageWeb 子文件夹
$ScriptDir = $PSScriptRoot
$ApiSourcePath = "$ScriptDir\LongShenStorageApi"
$WebSourcePath = "$ScriptDir\LongShenStorageWeb"

# ============================================================
# 颜色输出函数（使用英文标记避免编码问题）
# ============================================================
function Write-Success  { Write-Host "[OK] $args" -ForegroundColor Green }
function Write-Info     { Write-Host "[..] $args" -ForegroundColor Cyan }
function Write-Warn     { Write-Host "[!!] $args" -ForegroundColor Yellow }
function Write-Error    { Write-Host "[XX] $args" -ForegroundColor Red }
function Write-Step     { Write-Host "`n============================================" -ForegroundColor Magenta; Write-Host "  $args" -ForegroundColor Magenta; Write-Host "============================================" }

# 带超时的命令执行（防止卡死）
function Run-Command {
    param([string]$Command, [string]$WorkingDir, [int]$TimeoutSec = 120)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c $Command"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if ($WorkingDir) { $psi.WorkingDirectory = $WorkingDir }
    $p = [System.Diagnostics.Process]::Start($psi)
    if ($p.WaitForExit($TimeoutSec * 1000)) {
        $out = $p.StandardOutput.ReadToEnd()
        $err = $p.StandardError.ReadToEnd()
        return @{ ExitCode = $p.ExitCode; Output = $out; Error = $err }
    } else {
        $p.Kill()
        throw "Command timeout (>${TimeoutSec}s): $Command"
    }
}

# ============================================================
# 检查管理员权限
# ============================================================
Write-Step "Check admin permission"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warn "Some operations (Windows service, IIS) need admin rights."
    Write-Warn "Close this window, right-click PowerShell -> Run as Administrator"
    $confirm = Read-Host "Continue? (y/n)"
    if ($confirm -ne 'y') { exit }
}
Write-Success "Admin check done"

# ============================================================
# 1. 检查依赖
# ============================================================
Write-Step "1/5 - Check dependencies"

# 检查 .NET SDK
try {
    $dotnetVer = dotnet --version 2>$null
    Write-Success ".NET SDK $dotnetVer found"
} catch {
    Write-Error ".NET SDK not found! Please install .NET 10.0 SDK first."
    Write-Info "Download: https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
}

# 检查 IIS
$iisInstalled = $false
if (Get-Service -Name W3SVC -ErrorAction SilentlyContinue) {
    $iisInstalled = $true
}
if ($iisInstalled) {
    Write-Success "IIS found"
} else {
    Write-Warn "IIS not found. Frontend files will be copied only."
}

# 检查 SQL Server
try {
    $sqlTest = sqlcmd -S "$SqlServer" -U "$SqlUser" -P "$SqlPassword" -Q "SELECT @@VERSION" -h -1 -W 2>$null
    if ($sqlTest) {
        Write-Success "SQL Server connected OK"
    }
} catch {
    Write-Warn "SQL Server connect failed, will retry later"
}

Write-Success "Dependency check done"

# ============================================================
# 2. 初始化数据库
# ============================================================
if (-not $SkipDb) {
    Write-Step "2/5 - Init database"
    Write-Info "Connecting to $SqlServer ..."

    $sqlInit = @"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$DatabaseName')
BEGIN CREATE DATABASE [$DatabaseName]; PRINT 'DB created'; END
ELSE PRINT 'DB exists';
GO
USE [$DatabaseName];
GO
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AlertSettings' AND xtype='U')
CREATE TABLE AlertSettings (Id INT PRIMARY KEY DEFAULT 1 CHECK (Id=1), MinThreshold INT NOT NULL DEFAULT 2, MaxThreshold INT NOT NULL DEFAULT 18);
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StorageSlots' AND xtype='U')
CREATE TABLE StorageSlots (SlotCode NVARCHAR(50) PRIMARY KEY, IsOccupied BIT NOT NULL DEFAULT 0, WorkpieceId UNIQUEIDENTIFIER NULL, Zone NVARCHAR(50) NOT NULL DEFAULT '', RowNumber INT NOT NULL, ColumnNumber INT NOT NULL, LevelNumber INT NOT NULL);
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='WorkpieceRecords' AND xtype='U')
CREATE TABLE WorkpieceRecords (Id UNIQUEIDENTIFIER PRIMARY KEY, InboundTime DATETIME2 NOT NULL DEFAULT GETDATE(), SlotCode NVARCHAR(50) NOT NULL, LastOperator NVARCHAR(100) NOT NULL DEFAULT '', LastUpdated DATETIME2 NOT NULL DEFAULT GETDATE(), Notes NVARCHAR(500) NOT NULL DEFAULT '', PalletNumber NVARCHAR(50) NOT NULL DEFAULT '', ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '', ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '', ModelType NVARCHAR(200) NOT NULL DEFAULT '', WorkOrder NVARCHAR(200) NOT NULL DEFAULT '', CellNumber NVARCHAR(200) NOT NULL DEFAULT '', ComponentSections INT NOT NULL DEFAULT 1, CustomerName NVARCHAR(200) NOT NULL DEFAULT '');
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LedgerEntries' AND xtype='U')
CREATE TABLE LedgerEntries (Id UNIQUEIDENTIFIER PRIMARY KEY, Type INT NOT NULL, Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(), OperatorName NVARCHAR(100) NOT NULL DEFAULT '', SlotCode NVARCHAR(50) NOT NULL DEFAULT '', ActionDescription NVARCHAR(500) NOT NULL DEFAULT '', PalletNumber NVARCHAR(50) NOT NULL DEFAULT '', ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '', ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '', ModelType NVARCHAR(200) NOT NULL DEFAULT '', WorkOrder NVARCHAR(200) NOT NULL DEFAULT '', CellNumber NVARCHAR(200) NOT NULL DEFAULT '', ComponentSections INT NOT NULL DEFAULT 0, CustomerName NVARCHAR(200) NOT NULL DEFAULT '');
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
CREATE TABLE Users (Id UNIQUEIDENTIFIER PRIMARY KEY, Username NVARCHAR(100) NOT NULL UNIQUE, PasswordHash NVARCHAR(500) NOT NULL, Role INT NOT NULL DEFAULT 1, DisplayName NVARCHAR(100) NOT NULL DEFAULT '', IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE());
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DropdownOptions' AND xtype='U')
CREATE TABLE DropdownOptions (Category NVARCHAR(50) NOT NULL, Value NVARCHAR(200) NOT NULL, PRIMARY KEY (Category, Value));
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RolePermissions' AND xtype='U')
CREATE TABLE RolePermissions (RoleName NVARCHAR(50) NOT NULL, PageId NVARCHAR(50) NOT NULL, PRIMARY KEY (RoleName, PageId));
GO
IF NOT EXISTS (SELECT 1 FROM StorageSlots)
BEGIN
DECLARE @r INT=1,@c INT,@l INT;
WHILE @r<=2 BEGIN SET @c=1; WHILE @c<=4 BEGIN SET @l=1; WHILE @l<=8 BEGIN
INSERT INTO StorageSlots VALUES(CAST(@r AS NVARCHAR)+N'P-'+CAST(@c AS NVARCHAR)+N'C-'+CAST(@l AS NVARCHAR)+N'L',0,NULL,CAST(@r AS NVARCHAR)+N'P',@r,@c,@l); SET @l+=1; END SET @c+=1; END SET @r+=1; END
PRINT '64 slots created';
END
IF NOT EXISTS (SELECT 1 FROM AlertSettings) INSERT INTO AlertSettings VALUES(1,2,18);
GO
PRINT 'DB init done';
"@

    Write-Info "Creating database and tables (10-30 sec)..."
    try {
        # 将 SQL 写入临时文件，避免命令行引号转义问题
        $sqlFile = "$env:TEMP\deploy_init.sql"
        $sqlInit | Out-File -FilePath $sqlFile -Encoding ascii
        $connStr = "-S $SqlServer -U $SqlUser -P $SqlPassword"
        $result = Run-Command -Command "sqlcmd $connStr -i `"$sqlFile`" -b -I" -TimeoutSec 60
        Write-Host $result.Output
        if ($result.ExitCode -eq 0) {
            Write-Success "Database init done"
        } else {
            Write-Warn "sqlcmd warning: $($result.Error)"
        }
    }
    catch {
        Write-Error "Database init failed: $_"
        Write-Info "Skip DB init (--SkipDb), API will create tables on first run"
        $continue = Read-Host "Continue deploying API? (y/n)"
        if ($continue -ne 'y') { exit 1 }
    }
} else {
    Write-Info "Skip DB init"
}

# ============================================================
# 3. 发布后端 API
# ============================================================
if (-not $SkipApi) {
    Write-Step "3/5 - Publish API"

    $apiPublishPath = "$DeployPath\Api"

    if (-not (Test-Path $ApiSourcePath)) {
        Write-Error "API project not found: $ApiSourcePath"
        exit 1
    }

    if (Test-Path $apiPublishPath) {
        Remove-Item "$apiPublishPath\*" -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Info "Step A: dotnet restore..."
    try {
        $result = Run-Command -Command "dotnet restore" -WorkingDir $ApiSourcePath -TimeoutSec 180
        Write-Success "NuGet restore done"
    } catch {
        Write-Warn "NuGet restore timeout, trying offline publish..."
    }

    Write-Info "Step B: dotnet publish (may take 1-2 min)..."
    try {
        $result = Run-Command -Command "dotnet publish -c Release -o `"$apiPublishPath`" --self-contained false --no-restore" -WorkingDir $ApiSourcePath -TimeoutSec 300
        if ($result.ExitCode -ne 0) {
            Write-Error "Publish failed: $($result.Error)"
            exit 1
        }
        Write-Success "API published: $apiPublishPath"
    } catch {
        Write-Error "Publish timeout/failed: $_"
        exit 1
    }

    # 复制配置文件
    @("appsettings.json", "registers.json") | ForEach-Object {
        $src = "$ApiSourcePath\$_"
        $dst = "$apiPublishPath\$_"
        if (Test-Path $src) { Copy-Item $src $dst -Force; Write-Info "  Copied: $_" }
    }

    # 更新连接字符串
    $apiConfig = Get-Content "$apiPublishPath\appsettings.json" -Raw | ConvertFrom-Json
    $apiConfig.ConnectionStrings.DefaultConnection = "Server=$SqlServer;Database=$DatabaseName;User Id=$SqlUser;Password=$SqlPassword;TrustServerCertificate=True;"
    $apiConfig.Urls = "http://0.0.0.0:$ApiPort"
    $apiConfig | ConvertTo-Json -Depth 10 | Set-Content "$apiPublishPath\appsettings.json"
    Write-Info "Connection string updated"

    # 注册开机自启任务（替代 Windows 服务，更稳定）
    Write-Step "4/5 - Register auto-start task"

    # 停止旧进程
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "LongShenStorage" } | Stop-Process -Force 2>$null
    Start-Sleep -Seconds 1

    # 删除旧任务和服务
    schtasks /delete /tn "LongShenStorageApi" /f 2>$null
    & sc.exe delete "LongShenStorageApi" 2>$null

    $exePath = "$apiPublishPath\LongShenStorageApi.dll"
    if (Test-Path $exePath) {
        try {
            $dotnetPath = (Get-Command dotnet).Source
            $taskCmd = "dotnet `"$exePath`" --urls http://0.0.0.0:$ApiPort"
            schtasks /create /tn "LongShenStorageApi" /tr "$taskCmd" /sc onstart /ru SYSTEM /f
            Write-Success "Auto-start task [LongShenStorageApi] created"

            # 立即启动
            Write-Info "Starting API now..."
            schtasks /run /tn "LongShenStorageApi"
            Start-Sleep -Seconds 5

            # 验证是否启动成功
            $portCheck = netstat -ano | findstr ":${ApiPort} "
            if ($portCheck) {
                Write-Success "API is running on http://localhost:$ApiPort"
            } else {
                # 备用：直接启动
                Write-Warn "Task started but port $ApiPort not yet listening, starting directly..."
                $p = Start-Process -FilePath "dotnet" -ArgumentList "`"$exePath`" --urls http://0.0.0.0:$ApiPort" -WindowStyle Hidden -PassThru
                Write-Info "API PID: $($p.Id)"
            }
        } catch {
            Write-Warn "Task create failed: $_"
            Write-Info "Starting API directly..."
            $p = Start-Process -FilePath "dotnet" -ArgumentList "`"$exePath`" --urls http://0.0.0.0:$ApiPort" -WindowStyle Hidden -PassThru
            Write-Info "API PID: $($p.Id)"
        }
    } else {
        Write-Error "$exePath not found"
        Write-Info "Manual start: dotnet `"$apiPublishPath\LongShenStorageApi.dll`""
    }
} else {
    Write-Info "Skip API deploy"
}

# ============================================================
# 5. 部署前端
# ============================================================
if (-not $SkipFrontend) {
    Write-Step "5/5 - Deploy frontend"

    $frontendDst = "$DeployPath\Web"

    if (-not (Test-Path "$WebSourcePath\index.html")) {
        Write-Warn "Frontend source not found: $WebSourcePath\index.html"
    } else {
        Write-Info "Copying frontend files..."
        if (Test-Path $frontendDst) { Remove-Item "$frontendDst\*" -Recurse -Force -ErrorAction SilentlyContinue }
        New-Item -ItemType Directory -Path $frontendDst -Force | Out-Null
        Copy-Item "$WebSourcePath\*" $frontendDst -Recurse -Force
        Write-Success "Frontend deployed to: $frontendDst"

        # 更新 API 地址
        $jsFile = "$frontendDst\js\app.js"
        if (Test-Path $jsFile) {
            (Get-Content $jsFile) -replace "localhost:5000", "localhost:$ApiPort" | Set-Content $jsFile
            Write-Info "API address updated to localhost:$ApiPort"
        }

        Write-Info "Open directly: $frontendDst\index.html"
    }
} else {
    Write-Info "Skip frontend deploy"
}

# ============================================================
# 部署完成
# ============================================================
Write-Step "Deploy complete!"
Write-Success "=================================="
Write-Success "  Deploy path: $DeployPath"
if (-not $SkipApi) {
    Write-Success "  API: http://localhost:$ApiPort"
    Write-Success "  Swagger: http://localhost:$ApiPort/swagger"
}
if (-not $SkipFrontend) {
    Write-Success "  Frontend: $DeployPath\Web\index.html"
}
Write-Success "  Database: $SqlServer\$DatabaseName"
Write-Success "=================================="
Write-Info ""
Write-Info "Default accounts:"
Write-Info "  admin / admin123  (Admin - full access)"
Write-Info "  operator / 123456 (Operator)"
Write-Info "  viewer / 123456   (Viewer)"
Write-Info ""
Write-Info "Security: Change default passwords after first login!"
