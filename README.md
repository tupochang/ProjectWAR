# ProjectWAR

ProjectWAR 是《Warhammer Online: Age of Reckoning》的伺服器模擬專案。此 fork 記錄 Windows 本機部署步驟，以及 WorldServer 初始化與 Launcher 登入的修正。原始專案與英文說明可參考 [Shmerrick/ProjectWAR](https://github.com/Shmerrick/ProjectWAR) 和[原版英文 README](https://github.com/Shmerrick/ProjectWAR/blob/28e5fb24455301d97f0b150e68113b486b226249/README.md)。

## 環境

- Windows x64、Visual Studio 的「.NET 桌面開發」工作負載、.NET Framework 4.8 Developer Pack。
- MySQL Server 8.4、7-Zip、Git。可用 MySQL Workbench 執行 SQL。
- 自備完整的 Warhammer Online **1.4.8 Build 573** 客戶端，包含 `WAR.exe`、`data.myp` 和 `world.myp`。以完整副本進行測試，保留原始客戶端。

## 1. 初始化資料庫

1. 解開 [Database/Database.7z](Database/Database.7z)，依序在本機開發環境執行 `war_accounts.sql`、`war_characters.sql`、`war_world.sql`。依據 SQL 內的 `USE`，三份腳本分別使用 `war_accounts`、`war_characters` 和 `war_world_curated`。已有資料時先備份，初始 SQL 含建表與重建資料表指令。
2. 檢查 `war_accounts.realms`。本機驗收使用 `RealmId=1`、`Name=ProjectWAR`、`Adresse=127.0.0.1`、`Port=10300`。`RealmId` 必須與 `World.xml` 一致。
3. 檢查遊戲物件原型數量：

   ```sql
   SELECT COUNT(*) FROM war_world_curated.gameobject_protos;
   ```

   如果是剛匯入的基礎資料庫且此表為空，本次驗收使用的資料來源是 [Database/cooler_sai_sql_changes.7z](Database/cooler_sai_sql_changes.7z) 裡的 `sql_patches.7z`。從內層擷取 `2022-06-14_a91c2044.patch` 到空目錄，再於該目錄產生 SQL：

   ```powershell
   git apply --whitespace=nowarn .\2022-06-14_a91c2044.patch
   ```

   檢查產生的 `SQL/updates/war_world/2022_06_14_00_war_world_gameobject_protos.sql`。它包含 `DROP TABLE IF EXISTS`，會重建 `gameobject_protos`。只在已備份、適用的本機資料庫執行。先在 SQL 的本機副本把 `USE` 指向 `war_world_curated`，再匯入這一份 SQL。本次匯入後為 **3,131 筆**。其餘封存是供人工審閱的歷史補丁。

## 2. 編譯與放置設定

1. 開啟 `ProjectWAR.sln`，還原 NuGet 套件，選擇 **Release | x64**。建置 `Launcher`、`AccountCacher`、`LauncherServer`、`LobbyServer`、`WorldServer`。伺服器產物在 `bin\Release`，新版 Launcher 在 `bin\Release\Launcher`。若另外要編譯原生 `WarZone` 專案，需對應方案設定的 MSVC 工具組。
2. 將 [ImportIntoProject](ImportIntoProject) 的內容複製到 `bin\Release`，保留 `Abilities`、`zones`、`los`、`Configs` 目錄結構。首次部署時，把 [WorldServer/Configs/LocalDevelopment](WorldServer/Configs/LocalDevelopment) 的四個 XML 複製到 `bin\Release\Configs`。
3. 編輯執行目錄內的 XML。依本機 MySQL 設定填寫主機、使用者與密碼，並確認資料庫名稱：

   | 設定檔 | 檢查項目 |
   | --- | --- |
   | `Account.xml` | `AccountDB` → `war_accounts`，RPC `127.0.0.1:6800` |
   | `Launcher.xml` | RPC 指向 `127.0.0.1:6800`，Launcher TCP `8000` |
   | `Lobby.xml` | RPC 指向 `127.0.0.1:6800`，Lobby TCP `8048` |
   | `World.xml` | `CharacterDatabase` → `war_characters`，`WorldDatabase` → `war_world_curated`，`RealmId=1` |

4. 把新版 Launcher 產物放進完整測試副本的 `Launcher` 子目錄。沿用既有部署時，保留測試副本的 `Launcher.exe.config` 與本機登入 XML 設定。確認 `LaunchLocal` 和 `AutoLaunch` 為 `true`。將 [登入設定範本](ImportIntoProject/Configs/mythloginserviceconfig.xml) 放在 `Launcher.exe` 同一目錄，確認 `LoginServer` 的 `Address` 與 `Port` 是 `127.0.0.1`、`8048`。

## 3. 啟動與登入驗收

MySQL 啟動後，從 `bin\Release` 開四個終端機，依序啟動。每一步確認服務已監聽再開下一個。

| 順序 | 執行檔 | 本機連接埠 |
| --- | --- | --- |
| 1 | `AccountCacher.exe` | TCP 6800，內部 RPC |
| 2 | `LauncherServer.exe` | TCP 8000 |
| 3 | `LobbyServer.exe` | TCP 8048 |
| 4 | `WorldServer.exe` | TCP 10300，由 Realm 記錄指定 |

```powershell
Get-NetTCPConnection -State Listen -LocalPort 6800,8000,8048,10300 |
    Select-Object LocalAddress,LocalPort,OwningProcess,
        @{Name='Process';Expression={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

1. 在測試副本的 `Launcher` 目錄執行 `.\Launcher.exe`。使用本機測試帳號登入。現有 Launcher 會在測試副本內更新 `WAR.exe` 與 `data.myp`。
2. 檢查 Launcher 的 `logs` 是否記錄更新 `data/mythloginserviceconfig.xml` 成功。遊戲根目錄的 `logs/login.*.Log` 應記錄 `Attempting connection to 127.0.0.1:8048`。
3. 在 LobbyServer 主控台核對 `LobbyServer..Connection` 與 `CMSG_VerifyProtocolReq`。再確認是否顯示 `ProjectWAR` 並可到達角色選擇。

若登入後停在只有 `Server:` 的主選單，先核對本次遊戲日誌中的實際目的地，以及 `8048` 的程序是否為 `LobbyServer`。`8080` 是 Launcher 的背景檔案更新埠，請以 `8000` 的 Token 回應與 `8048` 的連線紀錄定位登入問題。

## 更新說明

參見 [CHANGELOG.md](CHANGELOG.md)。
