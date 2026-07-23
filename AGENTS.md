# 📌 專案概述 (視窗名: 截圖？截圖！)

本專案為一款針對 Windows 10/11 平台設計的 **WPF 輕量級高質感熱鍵截圖工具**。採用 C# / .NET 8 與 WPF 構建，極致注重 UI 美學（Fluent Design）與低延遲後台擷取體驗，並具備完善的防呆提示與互動式實時截圖畫廊。

---

## 🛠️ 技術棧與前置要求 (Technical Stack & Requirements)

* **目標平台**：Windows 10 (1809+) 以上（適配 High DPI 縮放）。
* **UI 框架**：WPF (.NET 8)，採用 MVVM 模式 (`CommunityToolkit.Mvvm`)。
* **字型與圖標規範**：
* 全域引進 `Segoe Fluent Icons` 字型資源。
* **所有圖標控制項必須明確指定**：`FontFamily="{StaticResource SegoeFluentIcons}"`。
* 圖標使用對應 Hex 碼（例如：`&#xE713;` 設定、`&#xE72C;` 重新整理、`&#xE768;` 開始偵測、`&#xE769;` 停止偵測、`&#xE8A7;` 開啟資料夾、`&#xE74D;` 刪除、`&#xE5C4;` 返回）。


* **視窗架構**：
* 單一視窗（Single Window Frame），**嚴禁彈出新視窗**。
* 所有子頁面（如「設定頁面」）必須在同一個視窗內透過 ViewModel 切換或平滑動畫淡入/淡出展示。



---

## 📐 視窗佈局與版面架構 (Strict Layout Architecture)

為了防止 UI 生成混亂，**必須嚴格遵循以下 XAML 骨架與雙欄區域編排**：

### 1. 主視窗架構 (`MainWindow.xaml`)

* **整體分層**：
* **Row 0 (Auto)**: 自訂 TitleBar (`Grid`)，僅擺放標題與右上角 `&#xE713;`（設定）、`-`（最小化）、`✕`（關閉）按鈕。
* **Row 1 (*)**: 主內容容器，包含**左側功能區**與**右側畫廊區**。
* **Overlay Layer (Grid.RowSpan="2")**: 未設定路徑時顯示的全螢幕/廣域半透明防呆遮罩。



### 2. 主內容區雙欄排版 (Main Content Grid Layout)

主內容區 (`Grid.Row="1"`) 必須劃分為左右雙欄：

* **`Grid.ColumnDefinitions`**:
* **`Column 0` (`Width="300"` 或 `280`)**: **左側控制面板區 (Left Panel)**。
* **`Column 1` (`Width="*"` )**: **右側截圖畫廊區 (Right Gallery Area)**。



---

## ⚙️ 視窗行為與功能規格 (Window & Functional Specifications)

### 1. 左側控制面板區 (Left Control Panel - `Grid.Column="0"`)

必須使用垂直的 `StackPanel` 或獨立 `Grid` 進行由上至下的佈局：

1. **目標視窗選取**：
* 標題與組合控制項：下拉選單 (`ComboBox`, `HorizontalAlignment="Stretch"`) + 重新整理按鈕（`&#xE72C;`）。
* 按下重新整理時，透過 Win32 API (`EnumWindows`) 遍歷當前活動視窗（須自動過濾無效、不可見或背景系統程序）。


2. **偵測開關**：
* 「開啟熱鍵偵測」切換按鈕/開關（開啟：`&#xE768;` / 關閉：`&#xE769;`）。
* 開啟後開始背景監聽，要求低延遲、按下即截圖。


3. **快捷按鈕**：
* 「開啟截圖資料夾」按鈕（`&#xE8A7;`），點擊後直接呼叫 `Process.Start` **開啟設定頁面中所指定的儲存資料夾**。



---

### 2. 右側截圖畫廊與預覽區 (Right Gallery Panel - `Grid.Column="1"`)

1. **排版與網格限制 (Strict 3-Column Grid Layout)**：
* 畫廊必須使用 `ScrollViewer` 包裹 `ItemsControl`。
* 圖片縮圖網格必須明確指定為 **1 行 3 列 (3 Columns Per Row)**：
```xml
<!-- ItemsPanelTemplate 必須明確限制為固定 3 欄 -->
<ItemsPanelTemplate>
    <UniformGrid Columns="3" HorizontalAlignment="Stretch"/>
</ItemsPanelTemplate>

```


* 按日期分組（如 `2026-07-22`），每個日期分組內部的縮圖清單皆強制遵循一行 3 張圖（`Columns="3"`）的排版規則。


2. **實時更新 (Real-time Watcher)**：
* 內部使用 `FileSystemWatcher` 監聽設定指定的截圖資料夾。
* 新增截圖時**自動非同步加載縮圖**並插入最前端，檔案被刪除時自動同步移除 UI 卡片。


3. **互動與檢視**：
* **鼠標Hover時**：預覽圖需要作出微放大效果，預覽圖外圈必須有明顯的被選取標示。
* **雙擊打開照片 (Double-click Preview)**：雙擊圖片卡片時，呼叫系統預設圖片檢視器 (`Process.Start` 圖片路徑) 快速放大開圖。
* **右鍵快顯功能表 (ContextMenu)**：提供帶有垃圾桶圖標（`&#xE74D;`）的「刪除截圖」選項，點擊後非同步刪除實體檔案並即時更新 UI。



---

### 3. 設定頁面 (Settings Panel - 平滑覆蓋)

* 點擊右上角 `&#xE713;` 後於原視窗內切換至設定頁，提供「返回按鈕（`&#xE5C4;`）」。
* **內容包含**：
1. **選擇截圖儲存資料夾**：`[ 路徑顯示框 ]` + `[ &#xE838; 瀏覽 ]` 按鈕，點擊呼叫 `OpenFolderDialog` 供使用者指定本機資料夾。完成指定後解鎖主畫面遮罩。
2. **鍵盤熱鍵錄製**：點擊後顯示「請按下按鍵...」，使用 Win32 API `RegisterHotKey` 進行全域鍵盤組合鍵註冊與動態錄製。
3. **手把熱鍵錄製**：點擊後顯示「請按下按鍵...」，使用 WinRT `Windows.Gaming.Input` 捕獲包含 Xbox `Home/Guide` 鍵在內的手把組合鍵。支援背景斷線自動重連機制。



---

### 4. 未設定路徑防呆遮罩 (Initial Setup Overlay)

* 當檢測到**尚未設定截圖儲存資料夾**時，主畫面（`Grid.Row="1"` 整個區域）必須被半透明磨砂遮罩覆蓋。
* 遮罩中央顯示警語：「**⚠️ 請先至右上角設定截圖存放資料夾**」，並停用主畫面所有操作，直到使用者完成資料夾指定。

---

### 5. 截圖技術規格 (Core Capture Engine)

* **後台擷取**：優先使用 WinRT `Windows.Graphics.Capture` API（Full Trust 環境），備用特化 `BitBlt` GDI 擷取。確保目標視窗被遮擋時仍可精準擷取。必須做到熱鍵被按下時能夠馬上截圖。
* **檔案命名規範**：
* 格式：`<window_name>_<timestamp_YYYYMMDD.HHmmSS.fff>.png`
* 範例：`BlackMythWukong_20260715.213045123.png`（毫秒級防衝突）。



---

## 🎨 UI/UX 視覺與美學規範 (Ugly UI Code Banned)

**為確保 Fluent Design 高質感，所有 XAML 程式碼必須遵守以下約束：**

### 📐 排版與安全防線

1. **禁止硬編碼絕對尺寸**：嚴禁對大容器指定固定的 `Width` 或 `Height`，使用 `*` 或 `Auto`。
2. **嚴禁元件重疊 (No Overlapping)**：
* **絕對禁止使用負數 Margins**（如 `Margin="-10,0,0,0"`）。
* 嚴禁在未劃分 `Row/ColumnDefinitions` 的同個 Grid 儲存格內堆疊多個控制項（遮罩廣域圖層除外）。
* 使用明確的 `Grid` 或 `StackPanel` / `WrapPanel` 包裹。


3. **文字防裁切**：控制項必須設定合理的 `Margin` 與 `Padding`。

### 🚫 禁用的設計細節

1. **禁止死板實色高對比邊框**（如 `BorderBrush="Black"`）。邊框應使用半透明微白/微黑線條（如 `#20FFFFFF`）。
2. **禁止純飽和度過高的背景**，使用深色質感背景（如 `#1E1E1E` 或 Fluent Acrylic 效果）。
3. **禁止控制項緊貼邊界**，外層統一保持 `12px~16px` Padding。
4. **禁止死板直角**：所有按鈕、Card、Border 必須設定圓角（`CornerRadius="6"` 或 `8`）。
5.  mandatory 動畫過渡：所有 Hover、Pressed 狀態切換必須透過 `VisualStateManager` 或 `Storyboard` 設定 `0.2s` 色彩淡入淡出漸變。

---

## 💻 程式碼架構與效能準則

1. **非同步優先 (Async/Await)**：所有檔案 I/O、圖檔載入/縮圖生成、視窗列舉皆須採用 `async/await`，絕不可阻塞 UI 主執行緒。
2. **Win32 P/Invoke 集中管理**：所有 Native API（如 `user32.dll`）必須統一封裝於 `NativeMethods.cs` 中，並具備完善的 try-catch 例外處理。
3. **記憶體管理與檔案解鎖**：預覽畫廊載入圖片縮圖時，必須使用 `BitmapCacheOption.OnLoad` 並適當解構 BitmapImage，確保截圖檔案不會被 WPF 程序鎖定，讓使用者能順利執行右鍵刪除操作。