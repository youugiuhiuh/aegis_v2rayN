# Spec: Overlay + 路徑探索 — V1

Status: **deferred**（2026-11-21：改採 `SYNC_TOKEN`，見 §15）
模式判定：**strict**（>3 檔案、CI 架構變更；依 AGENTS.md §1）

---

## 1. Objective

**要解決的問題**：上游 `2dust/v2rayN` 每次搬動專案或重寫 workflow，本 fork 的 release 會在幾小時後才以 `MSB1009: Project file does not exist` 爆掉，需要人工找路徑、改 workflow、用 SSH 推。

**使用情境**：維護 fork 的人（單人 + 自動排程同步）。

**成功長什麼樣**：
1. 上游再次搬動 `*.csproj`（改目錄／改名，同步更新 `.sln`）時，本 fork 的 CI **不需修改任何 workflow** 就能找到正確專案。
2. `git diff upstream/master..HEAD -- .github/workflows` 中，對上游檔案的改動 ≤ 2 檔、每檔 ≤ 20 行。
3. 不新增任何「上游 workflow 的完整副本」。
4. 五條 release pipeline（windows / linux / macos / windows-desktop / x86）行為不變：artifact 名稱、簽章、發佈條件都一樣。
5. `actionlint .github/workflows/*.yml` 0 error。

---

## 2. 現況與根因（證據）

| 事實 | 證據 |
|---|---|
| 上游把 WPF 專案搬家 | `2dust/v2rayN` `dad5813c`：`v2rayN/v2rayN/v2rayN.csproj` → `v2rayN/v2rayN.Windows/v2rayN.Windows.csproj` |
| 我們被打到 | run `44847713818`（release Windows）、`44847722640`（release Windows x86），皆 `MSB1009` |
| 為什麼 workflow 沒跟著更新 | workflow 權杖無法持有 `workflows` 權限，同步任務把 `.github/workflows/` 還原成同步前版本（`Preserve fork workflow files`） |
| 已做的處置 | `5f6796ce` 三處路徑改正；`8e1e04d4` 契約檢查 |
| 目前對上游 workflow 的偏離 | `build.yml` +14、`build-windows-x86.yml` ±10、`pub-key.yml` ±15、`upload-sign.yml` ±8、`build-all.yml` −14（另有 `build-windows-package.yml` 被凍結在外） |

**CodeGraph / MCP 證據（本次分析）**

- `query_graph`：`UseWPF` 只出現在 `v2rayN/v2rayN.Windows/v2rayN.Windows.csproj`；`AvaloniaResource` / `AvaloniaUseCompiledBindingsByDefault` 只出現在 `v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj`。→ 可以用 MSBuild 標記判別 flavor，不必靠檔名。
- `query_graph`：專案檔共 9 個（含 `ServiceLib`、`WebAPI`、`AmazTool`、兩個 Tests）。
- `codegraph node -f .github/workflows/build.yml`：YAML 被索引為 configuration/data file，**沒有** code 依賴邊 → 工作流之間的呼叫關係圖譜看不到，必須靠契約檢查（`scripts/check-ci-contract.py`）與 `actionlint` 補位。
- `get_graph_schema`：`Section` 節點是 Markdown 標題（可用來找既有 spec/plan），`Class` 節點包含 MSBuild 屬性名。

---

## 3. Capability map

| module id | 責任 | 依賴 |
|---|---|---|
| `project-discovery` | 由 MSBuild 標記解析 flavor → 專案路徑 | — |
| `build-integration` | `build.yml` 與各入口只傳 `flavor`，不再傳路徑字串 | `project-discovery` |
| `release-orchestration` | fork 自有的「release all platforms」，不再依賴凍結的上游 `build-all.yml` | `build-integration` |

建置順序：`project-discovery` → `build-integration` → `release-orchestration`。

---

## 4. 設計

### Layer 1：`scripts/ci-project.py`（新檔，fork-owned）

```
python3 scripts/ci-project.py wpf                    # ./v2rayN.Windows/v2rayN.Windows.csproj
python3 scripts/ci-project.py desktop --relative-to root
```

判別規則（皆已由 graph 證實唯一）：

| flavor | 條件 |
|---|---|
| `wpf` | `<UseWPF>true</UseWPF>` |
| `desktop` | 含 Avalonia 且 `<OutputType>WinExe</OutputType>`，且不是 wpf |
| `webapi` | `Microsoft.NET.Sdk.Web` 且路徑不含 `Tests` |
| `amaztool` | 檔名 `AmazTool.csproj` |

0 個或 ≥2 個符合 → **exit 2**（寧可紅燈，也不要拿錯 binary）。

### Layer 2：上游檔案的最小 hunk

| 檔案 | 改動 | 行數 |
|---|---|---|
| `build.yml` | 新增 `flavor` input；新增 `Resolve project path` step；兩個 `if:` 由字面路徑改為 `inputs.flavor == '...' \|\| (inputs.flavor == '' && inputs.project == '<原字面值>')`；兩個 build step 用 step 層 `env: Project:` 覆寫 | +12 / −2 |
| `build-windows.yml` | `project: ./v2rayN.Windows/...` → `flavor: wpf` | ±1 |
| `build-windows-x86.yml` | 新增 `resolve` job（輸出 matrix JSON），`package-x86.strategy.matrix: ${{ fromJSON(needs.resolve.outputs.matrix) }}` | +12 / −4 |
| `build-linux.yml`、`build-osx.yml`、`build-windows-desktop.yml` | 各 2 個呼叫點加 `flavor: desktop` | 各 +2 |

保留字面值 fallback 的理由：上游若把呼叫方也一起改，變基後仍能運作。

### Layer 3：`aegis-release-all.yml`（新檔，fork-owned）

手動入口，用 `gh workflow run` 派發五條 pipeline（名稱固定、不依賴上游 `build-all.yml` 的 curl 清單），沿用現在的 `release_tag` 慣例。

### 明確不做：`aegis-build.yml`

把 `build.yml` 抄一份自己養，會讓上游的每一項改進（.NET SDK 版本、runner image、AOT 旗標、artifact 命名、`upload-artifact` 版本）永遠不再進來，且腐化是無聲的。Yocto `.bbappend` 是追加幾個變數，不是抄一份 `.bb`。本 V1 只做「減少耦合」，不做「重造 pipeline」。

---

## 5. Commands

```bash
# 單元測試（新增）
python3 -m pytest tests/test_ci_project.py -q

# 契約檢查（既有）
python3 scripts/check-ci-contract.py

# 語法與運算式檢查
actionlint .github/workflows/build.yml .github/workflows/build-windows.yml \
           .github/workflows/build-windows-x86.yml .github/workflows/build-linux.yml \
           .github/workflows/build-osx.yml .github/workflows/build-windows-desktop.yml \
           .github/workflows/aegis-release-all.yml

# 端到端（不帶 tag，只建置不發佈）
gh workflow run "release Windows" --ref master
gh run watch
```

---

## 6. Project structure

```
scripts/ci-project.py                      新：flavor → 路徑
tests/test_ci_project.py                   新：discovery 的紅綠測試
.github/workflows/aegis-release-all.yml    新：fork 自有發佈入口
.github/workflows/build.yml                改：flavor + resolve step（hunk）
.github/workflows/build-windows.yml        改：flavor: wpf（hunk）
.github/workflows/build-windows-x86.yml    改：matrix 由 discovery 提供
.github/workflows/build-{linux,osx,windows-desktop}.yml  改：flavor: desktop
```

---

## 7. Code style（discovery 的核心片段）

```python
def resolve(flavour: str) -> Path:
    hits = [p for p in ROOT.glob("v2rayN/**/*.csproj") if FLAVOURS[flavour](p, read(p))]
    if len(hits) != 1:
        die(f"{len(hits)} projects match flavour {flavour!r}", hits)
    return hits[0]
```

---

## 8. Testing strategy

| 層級 | 內容 |
|---|---|
| 單元（TDD） | `tests/test_ci_project.py`：四個 flavor 各一筆、`--relative-to` 兩種錨點、0/多個候選時 exit 2。**先寫測試（紅）再寫實作（綠）** |
| 靜態 | `actionlint`（含 GitHub 運算式檢查）、`check-ci-contract.py` |
| 整合 | `gh workflow run "release Windows"`（無 tag）→ 兩個 matrix 的 `Build v2rayN WPF` 必須 success |
| 回歸 | 比對 artifact 名稱與既有 pipeline 一致（`x64`、`arm64`、`x64-aot`…） |

x86 只有帶 `release_tag` 才會執行，V1 只能驗到 `resolve` job（matrix 產生正確）。

---

## 9. Boundaries

- **Always**：推送前跑 `actionlint` + `check-ci-contract.py`；每個 hunk 單獨 commit；先測試後實作。
- **Ask first**：新增/刪除 workflow 檔；動 `upload-sign.yml` / `pub-key.yml`（簽章）；改 release 觸發條件。
- **Never**：整檔複製上游 workflow；改 `.sln`；在這次一併改簽章或發佈流程；在 `build.yml` 引入新的路徑字面值。

---

## 10. Success criteria（可測）

- [ ] `tests/test_ci_project.py` 全綠，且「把 `v2rayN.Windows` 改名後仍能找到」有對應測試案例。
- [ ] `actionlint` 0 error；`check-ci-contract.py` exit 0。
- [ ] `release Windows` dispatch 綠燈，`Build v2rayN WPF` 與 `Build AmazTool` 都 success。
- [ ] `git diff upstream/master..HEAD -- .github/workflows` 對上游檔案的改動 ≤ 2 檔、每檔 ≤ 20 行。
- [ ] 沒有新增 `aegis-build.yml` 之類的整檔副本。

---

## 11. Out of scope（Deferred）

- `SYNC_TOKEN` / GitHub App（路線 B）——獨立變更，與本 spec 無關。
- 把 `build-all.yml`、`build.yml` 等還原成上游版本（V2，等 V1 綠燈後再評估）。
- fork 化 `package-zip.yml` / `upload-sign.yml` / `pub-key.yml`。
- 上游新增的 `build-windows-package.yml` 流程。
- 停用上游入口 workflow（`gh workflow disable`）。

---

## 12. Risks

| 風險 | 緩解 |
|---|---|
| `build.yml` 的 `if:` 與上游改同一行 → 變基衝突 | 保留字面值 fallback；rerere 記憶；衝突時 issue 告警 |
| discovery 抓到錯的專案 | 0/多個候選直接 exit 2，不猜 |
| `fromJSON` matrix 若格式錯 → job 直接失敗 | 由 script 產生 JSON，並在單元測試斷言格式 |
| x86 無法端到端驗證 | 只驗 resolve job；下次帶 tag 的 release 視為驗收 |

---

## 13. Open questions（需要你回答）

1. x86 的 matrix：用 `fromJSON(needs.resolve.outputs.matrix)`（單一輸出）還是兩個獨立 output？我傾向 `fromJSON`。
2. 要不要保留「單一手動入口」（`aegis-release-all.yml`），還是繼續用上游的 `release all platforms`？
3. 是否接受在 `build-linux.yml` / `build-osx.yml` / `build-windows-desktop.yml` 各加 2 行 `flavor: desktop`？（不加也能運作，但上游改名時這三條就不會自動跟上。）
4. 驗證授權：可否讓我 dispatch 一次 `release Windows`（不帶 `release_tag`，只建置、不發佈）？

---

## 14. 實作流程（批准後才進行）

strict 模式：`git worktree` 隔離 → `planning-and-task-breakdown` 拆成 2–5 分鐘任務 → 並行子代理（`project-discovery` 與 `build-integration` 檔案不重疊）→ TDD → `code-review-and-quality` → 推送與驗證。

---

## 15. 決策紀錄（2026-11-21）：改採 `SYNC_TOKEN`，本 spec 擱置

改以路線 B 取代，不進行本 spec 的實作：

- repo secret `SYNC_TOKEN`（PAT，含 `workflow` scope）已設定；
- `upstream-sync.yml` 既有實作即生效：`Preserve fork workflow files` 自動跳過，上游 workflow 變更（含專案改名）隨變基進入；
- 已接受副作用：PAT 的 push 會觸發下游 release 建置。

**保留的價值**：§2 的 CodeGraph 證據（`UseWPF` / `AvaloniaResource` 的唯一性）與 §13 的 discovery 規則，在以下情況可重新啟用本 spec：

1. PAT 無法延續（政策變更、到期不再續、人員變動）；
2. 上游 workflow 改動開始與 fork 的 hunk 頻繁衝突，rerere 也無法收斂；
3. 決定把 fork 的 CI 完全去耦（overlay）。
