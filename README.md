# ProjectWAR

ProjectWAR 是《Warhammer Online: Age of Reckoning》的伺服器模擬專案。此 fork 記錄 Windows 本機部署步驟，以及 WorldServer 初始化與 Launcher 登入的修正。原始專案與英文說明可參考 [Shmerrick/ProjectWAR](https://github.com/Shmerrick/ProjectWAR) 和[原版英文 README](https://github.com/Shmerrick/ProjectWAR/blob/28e5fb24455301d97f0b150e68113b486b226249/README.md)。

## 環境

- Windows x64、Visual Studio 的「.NET 桌面開發」工作負載、.NET Framework 4.8 Developer Pack。
- MySQL Server 8.4、7-Zip、Git。可用 MySQL Workbench 執行 SQL。
- 自備完整的 Warhammer Online **1.4.8 Build 573** 客戶端，包含 `WAR.exe`、`data.myp` 和 `world.myp`。以完整副本進行測試，將 `WAR.exe`、`data.myp`、原 Launcher 與設定的備份放在固定目錄。

## 1. 初始化資料庫

1. 解開 [Database/Database.7z](Database/Database.7z)，開啟 MySQL Workbench 並連線到本機 MySQL。用 **File → Open SQL Script** 逐一開啟 `war_accounts.sql`、`war_characters.sql`、`war_world.sql`，各按一次工具列的閃電圖示執行整份腳本。重新整理左側 **Schemas**，確認三個資料庫都出現。依據 SQL 內的 `USE`，三份腳本分別使用 `war_accounts`、`war_characters` 和 `war_world_curated`。已有資料時先備份，初始 SQL 含建表與重建資料表指令。
2. 檢查 `war_accounts.realms`。本機驗收使用 `RealmId=1`、`Name=ProjectWAR`、`Adresse=127.0.0.1`、`Port=10300`。`RealmId` 必須與 `World.xml` 一致。
3. 檢查遊戲物件原型數量：

   ```sql
   SELECT COUNT(*) FROM war_world_curated.gameobject_protos;
   ```

   如果是剛匯入的基礎資料庫且此表為空，本次驗收使用的資料來源是 [Database/cooler_sai_sql_changes.7z](Database/cooler_sai_sql_changes.7z) 裡的 `sql_patches.7z`。從內層擷取 `2022-06-14_a91c2044.patch` 到空目錄，再於該目錄產生 SQL：

   ```powershell
   git apply --whitespace=nowarn .\2022-06-14_a91c2044.patch
   ```

   檢查產生的 `SQL/updates/war_world/2022_06_14_00_war_world_gameobject_protos.sql`。它包含 `DROP TABLE IF EXISTS`，會重建 `gameobject_protos`。只在已備份、適用的本機資料庫執行。先在 SQL 的本機副本把 `USE` 指向 `war_world_curated`，再匯入這一份 SQL。

   本機驗收前，已補上原本為空的 `gameobject_protos`，匯入後查到 **3,131 筆**。新的部署可用同一段 `SELECT COUNT(*)` 確認結果。其餘封存是供人工審閱的歷史補丁。

## 2. 編譯與放置設定

1. 開啟 `ProjectWAR.sln`，還原 NuGet 套件，選擇 **Release | x64**。建置 `Launcher`、`AccountCacher`、`LauncherServer`、`LobbyServer`、`WorldServer`。伺服器產物在 `bin\Release`，新版 Launcher 在 `bin\Release\Launcher`。若另外要編譯原生 `WarZone` 專案，需對應方案設定的 MSVC 工具組。若出現找不到 `NLog`，先確認 Launcher 專案的 NuGet 還原成功，再重新建置。
2. 將 [ImportIntoProject](ImportIntoProject) 的內容複製到 `bin\Release`，保留 `Abilities`、`zones`、`los`、`Configs` 目錄結構。

### 下載並放置已修正的 XML

1. 從 [LocalDevelopment 範本目錄](WorldServer/Configs/LocalDevelopment) 下載下表的四個 XML。從 GitHub 開啟檔案後可用 **Download raw file**，已下載整份專案的話，可直接使用其中的 `WorldServer\Configs\LocalDevelopment`。
2. 把四個檔案複製到 **`bin\Release\Configs`**。已有本機設定時，先備份原檔。確認同一目錄也有上一步複製的 `mythloginserviceconfig.xml`，LauncherServer 啟動時會讀取它。
3. 開啟執行目錄內的 `Account.xml` 和 `World.xml`，將 `YOUR_MYSQL_PASSWORD` 換成自己的 MySQL 密碼。`Account.xml` 要填一處，`World.xml` 的 `CharacterDatabase` 和 `WorldDatabase` 要各填一處。如果兩段使用同一個 MySQL 帳號，兩處都填相同的密碼。範本預設使用 `root` 和 `127.0.0.1:3306`。使用不同的 MySQL 帳號、主機或連接埠時，同時更新對應欄位。

| 檔案 | 範本已套用的修正或設定 |
| --- | --- |
| [Account.xml](WorldServer/Configs/LocalDevelopment/Account.xml) | 根節點修正為 `AccountConfig`。使用 `war_accounts`，RPC 埠為 `6800`。 |
| [Launcher.xml](WorldServer/Configs/LocalDevelopment/Launcher.xml) | 補齊 `</Message>`。Launcher 埠為 `8000`。 |
| [Lobby.xml](WorldServer/Configs/LocalDevelopment/Lobby.xml) | 本機 Lobby 埠為 `8048`，客戶端版本為 `1.4.8`。 |
| [World.xml](WorldServer/Configs/LocalDevelopment/World.xml) | 已指向 `war_characters` 和 `war_world_curated`。`RealmId` 預設為 `1`，須與 `war_accounts.realms` 的本機 Realm 一致。 |

四個範本均已設定 `IConfiguredTheFile=true`。

### 使用含啟動修正的建置產物

請用本 fork 建置的 `WorldServer.exe` 和 `Launcher.exe`。WorldServer 現在會先建立高階與低階的活動戰區 Region，再掛載 Campaign。戰場目標清除發光物件時，若 Region 還沒就緒，會記錄警告並返回。Launcher 會更新 Build 573 真正讀取的 `data/mythloginserviceconfig.xml`。這些修正已在原始碼裡，依上一節建置即可套用。[更新說明](CHANGELOG.md)

### 將新版 Launcher 放到測試客戶端

從 `bin\Release\Launcher` 複製新版產物到完整遊戲副本的 `Launcher` 子目錄。若已經有可用的 Launcher 部署，先備份該子目錄，再替換 `Launcher.exe`，保留原有的 `Launcher.exe.config` 和登入 XML。確認 `LaunchLocal`、`AutoLaunch` 為 `true`。

客戶端的 `mythloginserviceconfig.xml` 必須和 `Launcher.exe` 放在**同一層**。使用[登入設定範本](ImportIntoProject/Configs/mythloginserviceconfig.xml)，在 `<LoginServer>` 裡確認以下值。新版 Launcher 會用這份 XML 更新 `data.myp` 的既有登入資源。

```xml
<Address>127.0.0.1</Address>
<Port>8048</Port>
```

## 3. 啟動與登入驗收

MySQL 啟動後，開四個 PowerShell 視窗，每個視窗都先切到專案的 `bin\Release` 目錄。依序執行下表的指令。等目前服務顯示正在監聽，再啟動下一個。

| 順序 | 執行檔 | 本機連接埠 |
| --- | --- | --- |
| 1 | `.\AccountCacher.exe` | TCP 6800，內部 RPC |
| 2 | `.\LauncherServer.exe` | TCP 8000 |
| 3 | `.\LobbyServer.exe` | TCP 8048 |
| 4 | `.\WorldServer.exe` | TCP 10300，由 Realm 記錄指定 |

```powershell
Get-NetTCPConnection -State Listen -LocalPort 6800,8000,8048,10300 |
    Select-Object LocalAddress,LocalPort,OwningProcess,
        @{Name='Process';Expression={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

首次使用可由 Launcher 的 **Create Account** 建立本機測試帳號。如果畫面回應不明確，可用以下唯讀查詢確認帳號是否建立。查不到資料時，檢查 LauncherServer 的 `CL_CREATE` 紀錄及 AccountCacher 的資料庫設定。分享日誌前請移除帳密、雜湊與 Token。

```sql
SELECT AccountId, Username
FROM war_accounts.accounts
WHERE Username = 'your_test_account';
```

1. 在測試副本的 `Launcher` 目錄執行 `.\Launcher.exe`。使用本機測試帳號登入。現有 Launcher 會在測試副本內更新 `WAR.exe` 與 `data.myp`。
2. 檢查 Launcher 的 `logs` 是否記錄更新 `data/mythloginserviceconfig.xml` 成功。遊戲根目錄的 `logs/login.*.Log` 應記錄 `Attempting connection to 127.0.0.1:8048`。
3. 在 LobbyServer 主控台核對 `LobbyServer..Connection` 與 `CMSG_VerifyProtocolReq`。再確認是否顯示 `ProjectWAR` 並可到達角色選擇。

若登入後停在只有 `Server:` 的主選單，先核對本次遊戲日誌中的實際目的地，以及 `8048` 的程序是否為 `LobbyServer`。`8080` 是 Launcher 的背景檔案更新埠，請以 `8000` 的 Token 回應與 `8048` 的連線紀錄定位登入問題。

## 4. 把遊戲介面切換成繁體中文

1. 先用角色進入遊戲，打開聊天視窗。
2. 輸入 `/language 8`（也可用 `/L 8`），按 Enter。
3. 完全關閉遊戲，再從 Launcher 啟動一次。

切換會使用客戶端已有的繁中資源。介面變成中文後，伺服器提供的任務和 NPC 文字仍可能是英文。若要切回英文，輸入 `/language 1` 後重啟。[語言指令與重啟方式](https://returnofreckoning.com/forum/viewtopic.php?t=9252) · [繁中指令的社群紀錄](https://forum.gamer.com.tw/C.php?bsn=8836&snA=25878)

## 5. 角色連線後卡在載入畫面

這次部署的 WorldServer 日誌出現 `F_CONNECT User connection`，接著記錄 `AddObject` 的 Zone 6 錯誤。這表示角色連線已到世界服務，但尚未成功加入地圖。先核對角色存檔與區域資料，再檢查地圖資源。

```sql
SELECT c.CharacterId, v.ZoneId, v.RegionId AS SavedRegion, z.Region AS ZoneRegion
FROM war_characters.characters AS c
JOIN war_characters.characters_value AS v ON v.CharacterId = c.CharacterId
LEFT JOIN war_world_curated.zone_infos AS z ON z.ZoneId = v.ZoneId
WHERE c.Name = 'your_character';
```

- `ZoneRegion` 為 `NULL` 時，先查是否缺少該 Zone 定義。
- `SavedRegion` 和 `ZoneRegion` 不同時，先檢查角色存檔位置與 Zone 定義。
- 兩者相同時，檢查目前的 Region 是否已載入該 Zone。[角色進入世界的程式碼](WorldServer/NetWork/Handler/MovementHandlers.cs) · [Region 加入物件的程式碼](WorldServer/World/Map/RegionMgr.cs)

WorldServer 預設從工作目錄下的 `zones/` 讀取地圖資料，從 `los/` 讀取視線遮蔽資料。以 Zone 6 為例，檢查 `bin/Release/zones/zone006/` 內的 `offset.png`、`terrain.png`，以及 `bin/Release/los/6.bin`。若缺地圖檔，原作者的 Zone Data v1 發布頁提供 `zones.zip`。解壓時保留 `zoneNNN` 子目錄，並確認 `ZoneFolder` 指向 WorldServer 執行時可讀取的位置。[設定範本](WorldServer/Configs/LocalDevelopment/World.xml) · [原作者 Zone Data v1](https://github.com/Shmerrick/ProjectWAR/releases/tag/zones-data-v1)

目前待排查的項目包括 Zone 6 角色載入、`KeepNPC No FlagGuard Proto`、無效的生成資料與部分技能資料。若出現 `RegionOcclusionFolder` 警告，核對 LOS 設定和資料路徑。繁體中文化是後續工作，需分別驗證客戶端語言資源及伺服器提供的任務與 NPC 文字。

## 更新說明

參見 [CHANGELOG.md](CHANGELOG.md)。
