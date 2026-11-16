# 更新說明

## 2026-11-16

### Launcher 登入

- WAR.exe 1.4.8 Build 573 啟動後曾停在主選單。遊戲日誌顯示連往 `47.198.78.194:8048`，而本機 LobbyServer 設定為 `127.0.0.1:8048`。
- 舊版 Launcher 將本機 XML 寫入固定 ID `0x0B3E7AC0C6762BF7`。Build 573 對應的資源路徑是 `data/mythloginserviceconfig.xml`，由 `MYP.HashWAR()` 算得 `0x3FE03665349E2A8C`。新版以該路徑找出既有項目，缺少項目時會中止並寫入診斷日誌。
- `UpdateWarData()` 使用 Launcher 安裝目錄定位 XML 和遊戲根目錄的 `data.myp`，記錄更新結果，並確保 MYP 檔案資源釋放。

### WorldServer 初始化

- 在將 Campaign 掛到 Region 前，先建立高階與低階的活動 Battlefront Region。
- BattlefieldObjective 的 `RemoveGlow()` 遇到 Region 尚未就緒時，會寫入警告並返回，以避免空參考例外。
- 本機部署另補入 `war_world_curated.gameobject_protos` 的 3,131 筆原型。資料來源與匯入方式見 [README](README.md#1-初始化資料庫)。

### 驗收

- Launcher、AccountCacher、LauncherServer、LobbyServer、WorldServer 已在乾淨的 fork 檢出套用本次程式差異後完成 Release x64 建置。
- 獨立客戶端副本更新後，重新開啟 MYP，從目標路徑讀回 `127.0.0.1:8048`。資源索引維持 4,868 筆。目標資源缺失時可重開測試檔，且 SHA-256 維持相同。
- 維護者以完整客戶端手動驗收，回報可成功登入。後續部署紀錄顯示角色連線已到 WorldServer，Zone 6 的地圖載入仍待排查。
- README 補上首次帳號建立、四份 XML 的逐步修改（含 Account 根節點與 Launcher 的 Message 結束標籤）、Zone／LOS 路徑及角色位置的唯讀檢查。
- 部署步驟明列 `World.xml` 的兩個 MySQL 密碼欄位、已包含的啟動修正、3,131 筆原型的補齊方式，以及客戶端繁體中文指令。
