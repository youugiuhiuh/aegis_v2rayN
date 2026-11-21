# 补丁堆栈（Patch Stack）与自动化上游同步

本文说明本仓库与上游 `2dust/v2rayN` 的长期同步模型、目前为什么仍会周期性地需要人工修复，
以及把「每次都要修」变成「几乎不用修」的具体做法。

相关实现：

- `.github/workflows/upstream-sync.yml` — 同步任务
- `scripts/upstream-sync.sh` — 变基与 `git rerere` 缓存管理
- `scripts/check-ci-contract.py` — 工作流路径契约检查
- `docs/UPSTREAM_SYNC.md` — 日常操作手册

---

## 1. 同步模型

```
master = upstream/master + 一串 fork 补丁（每个提交 = 一个逻辑补丁）
```

每次同步就是把 fork 的补丁栈整体变基到新的 `upstream/master` 上：

```
git rebase upstream/master      # 重放补丁栈
git push --force-with-lease     # 用线性历史覆盖 master
```

这本身就是「补丁堆栈」的做法，`git rerere` 充当冲突解决记忆：

- 同一处冲突第二次出现时，Git 自动套用上次的解法；
- 首次出现的冲突则中止变基，由 `Report Sync Failure` 创建 Issue。

因此「同步」不是难点，**难点在于补丁栈的形态**：

| 补丁形态 | 冲突概率 | 说明 |
| --- | --- | --- |
| add-only（只新增文件） | 极低 | 上游永远不会碰这些路径，例如 `scripts/`、`docs/`、`aegis_v2rayN-public-key.asc` |
| hunk 型（在上游文件里加一小段） | 中 | 例如在 `build.yml` 里加一个 smoke test 步骤；上游改动邻近代码时才会冲突 |
| 整文件替换 | 高 | 例如把整个 workflow 冻结成某天的版本；上游任何重命名/重构都会让快照失效 |

`scripts/check-ci-contract.py` 就是为最后一行准备的。

---

## 2. 为什么会「每次都要修」

### 2.1 推送权限把 workflow 文件冻结了

`GITHUB_TOKEN` 是 GitHub App 安装令牌，`permissions:` 里没有也不可能有 `workflows`
权限。任何会创建或修改 `.github/workflows/*` 的推送都会被服务端拒绝：

```
! [remote rejected] HEAD -> master (refusing to allow a GitHub App to create or update
  workflow `.github/workflows/build-all.yml` without `workflows` permission)
```

于是同步任务只能把 `.github/workflows/` 恢复成同步前的版本再推送（`Preserve fork
workflow files` 步骤）。代码和脚本会跟着上游走，**workflow 文件不会**。

### 2.2 冻结的 workflow 里写死了路径

上游移动了 WPF 工程：

```
v2rayN/v2rayN/v2rayN.csproj  ->  v2rayN/v2rayN.Windows/v2rayN.Windows.csproj
```

`v2rayN.sln`、`package-windows.ps1` 等文件被变基更新了，但三份被冻结的 workflow 仍然指向
旧路径，于是 `Build v2rayN WPF` 报 `MSB1009: Project file does not exist`。

同类隐患还包括：上游新增/删除工件、改名 `dotnet` 版本、更改输出目录、拆分 workflow、
新增必需 secret 等。它们都表现为「同步成功、构建失败」，而且往往要等到下一次 release 才暴露。

### 2.3 补丁与上游改动重叠

fork 改过的上游文件（`build.yml` 的 smoke test、`upload-sign.yml` 的 GPG、`pub-key.yml`
等）在上游同一处改动时会冲突。冲突本身是正常的补丁栈行为，问题在于**没人知道这次冲突属于
哪个补丁、影响什么**。

---

## 3. 现状：契约检查（已实现）

`scripts/check-ci-contract.py` 在同步任务的最后一步运行：

1. 扫描 `.github/workflows/*.yml` 里所有 `*.csproj` 引用；
2. 按两种基准解析（仓库根目录 / 复用工作流的 `working-directory: ./v2rayN`）；
3. 解析不到的引用以 `::error file=...,line=...` 输出，并给出候选：
   按 `<AssemblyName>` 匹配（`v2rayN.Windows.csproj` 的 AssemblyName 仍是 `v2rayN`），
   同时给出根目录形式与 `v2rayN/` 相对形式；
4. 报告写入 `$GITHUB_STEP_SUMMARY` 与 `$RUNNER_TEMP/ci-contract.md`，
   失败时由 `Report Sync Failure` 并入 Issue。

效果：重命名当天就能在**同步任务**里看到精确的文件行号与替换候选，而不是几天后在一次
release 构建里看到一个 MSB1009。它只负责发现，不负责修复 —— 因为修复同样需要推送
workflow 变更。

本地运行：

```bash
python3 scripts/check-ci-contract.py              # 只检查
python3 scripts/check-ci-contract.py --report /tmp/ci-contract.md
```

---

## 4. 三条演进路线

### 路线 A：保持快照（当前状态）

- 优点：不需要任何额外凭据；workflow 完全由本仓库控制。
- 缺点：上游每次改 workflow 或移动工程，都要人工同步一次。
- 适用：暂时无法配置 PAT 时的过渡状态。

### 路线 B：配置 `SYNC_TOKEN`（推荐，一次性投入）

1. 生成 classic PAT，勾选 `workflow` scope（或细粒度 token：**Workflows: Read and write** + 仓库 Contents: Read and write）；
2. 仓库 `Settings → Secrets and variables → Actions` 新建 secret `SYNC_TOKEN`；
3. 不需要改任何代码 —— 同步任务已经支持：

   ```yaml
   token: ${{ secrets.SYNC_TOKEN || secrets.GITHUB_TOKEN }}
   ```

   配置后 `Preserve fork workflow files` 步骤自动跳过（`if: env.SYNC_TOKEN == ''`）。

得到的效果：

- 上游的 workflow 重命名、新增、删除会随变基一起进入 fork；
- 上游重命名工程时，`build.yml` 的 `if:` 与 `project:` 自动一起更新，不再需要人工修；
- fork 的 hunk 型补丁（例如 smoke test）由变基自动重放到新版本上，只有真正重叠才会冲突，
  冲突由 rerere 记忆并由 Issue 报告。

这是把「每次都要修」降到「只在真正语义冲突时修」的关键一步。

### 路线 C：结构性去重叠（长期）

目标：让 fork 对上游文件的修改趋近于零。

1. **fork 专属文件化**：把自定义 CI 放在上游永远不会创建的文件里，例如
   `aegis-smoke.yml`、`aegis-release-guard.yml`，或封装成 `.github/actions/*` 复合动作；
2. **复用工作流作为边界**：fork 的入口 workflow（fork 自有）`workflow_call` 上游的
   `build.yml`，把额外步骤放在自己的 job 里，而不是插进上游文件；
3. **路径不写死**：需要工程路径时用 `needs.*.outputs` 传递由脚本发现的结果
   （例如从 `v2rayN.slnx` 解析），而不是在每个 workflow 里写常量；
4. **补丁台账**：`git log` 之外，每个改上游文件的补丁在提交正文里写明
   `Upstream-files:`、`Why:`、`Remove-when:`，便于上游重构后快速判断谁需要调整。

完成路线 C 后，补丁栈里剩下的几乎都是 add-only 补丁，同步可以长期无人值守。

---

## 5. 发生重命名/冲突时的处理顺序

1. 打开失败的同步任务，读 `ci-contract.md` 报告或 Issue；
2. 按报告里的 `file:line` 与候选路径修改 workflow（注意两种基准：
   `build.yml` 及其调用方是相对 `./v2rayN`，其它是相对仓库根目录）；
3. 本地验证：

   ```bash
   python3 scripts/check-ci-contract.py
   /path/to/actionlint .github/workflows/*.yml
   ```

4. 用 SSH key 或带 `workflow` 权限的 token 推送（`GITHUB_TOKEN` 推不了 workflow 变更）；
5. 触发一次 `release Windows` / `Code Test` 确认构建通过；
6. 如果是首次出现的补丁冲突，把解法留在历史里（rerere 会在下次自动套用），
   并在提交正文补一句原因，避免下一个人重新推导。

---

## 6. 验收清单

- [ ] `python3 scripts/check-ci-contract.py` 退出码为 0；
- [ ] `actionlint .github/workflows/*.yml` 无报错；
- [ ] 同步任务 `Upstream Sync` 为绿色，且 `Verify CI path contract` 步骤通过；
- [ ] 至少一次 `release Windows` 绿色；
- [ ] （建议）`SYNC_TOKEN` 已配置，`Preserve fork workflow files` 步骤显示 skipped。
