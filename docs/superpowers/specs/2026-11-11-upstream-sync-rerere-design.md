# Design Doc: 自动化上游同步与 Git Rerere 线性补丁堆栈

## 1. 意图与背景 (Intent & Why)
`aegis_v2rayN` 是基于上游 `2dust/v2rayN` 的下游定制维护分支（包含了 xdrive 驱动、OneDrive 模板、预设参数、CI 冒烟等改动）。
目前每次同步上游需要手动拉取或进行复杂的合并操作。为了最小化日常维护心智负担，同时保证代码与上游主干持续保持同步，需要一套自动化机制：
在无冲突（或已有解决冲突记录）时，自动完成变基并合流到 `master`；唯有发生不可自动解决的代码冲突时才阻断流程并提醒人工处理。

---

## 2. 目标与非目标 (Goals, Non-Goals & Anti-Goals)

### 目标 (Goals)
1. **全自动同步**：支持 GitHub Actions 定时触发（Cron）与手动触发（`workflow_dispatch`），自动拉取 `2dust/v2rayN:master`。
2. **冲突时安全阻断**：变基过程中若遇到无法自动解决的代码冲突，立即安全中止（`git rebase --abort`），严禁强推损坏的代码，并创建/更新 Issue 提示人工介入。
3. **Git Rerere 历史自愈能力**：将 Git 的冲突重用记录（`.git/rr-cache`）持久化存储于独立的 `rerere-cache` 孤儿分支。一旦某次冲突被人为解决并记录，后续重复遭遇该冲突时自动重用解法。
4. **轻量与易维护**：无需拆分多分支补丁树，所有开发者日常只聚焦于一个 `master` 分支，保持简单。

### 非目标 (Non-Goals)
- 不拆分数十个单独的 `patch/*` 功能子分支（避免高昂的日常分支维护与切分支成本）。
- 不引入外部庞大且不易排错的重型第三方复合 Action，采用纯原生 Git 指令 + 独立 Bash 脚本配合官方 Action 完成。

### 反目标 (Anti-Goals)
- 绝不在有未决冲突或测试编译失败时强制覆写 `master`。
- 绝不在 CI 失败时污染本地工作区或破坏现有 Git 树。

---

## 3. 方案对比与选型 (Approaches Considered)

### 备选方案 1：多分支 Patch Stack（krlmlr / DJRHails 模式）
- **机制**：每个定制功能单独一个 `patch/*` 分支，CI 每次拉取上游后重新构建并拓扑重放所有 patch 分支。
- **缺点**：维护成本极高，日常每次开发小修改都要新建或切换 patch 分支，且分支依赖顺序易错乱。

### 备选方案 2：单分支线性 Rebase 堆栈 + Git Rerere 缓存（已选定方案）
- **机制**：
  - 核心定制代码以线性 commit 堆叠在 `upstream/master` 之上。
  - CI 开启 `rerere.enabled` 与 `rerere.autoupdate`。
  - 同步前从 `rerere-cache` 分支拉取历史解冲突记录到 `.git/rr-cache`。
  - 执行 `git rebase upstream/master`。
  - 成功时更新 `rerere-cache` 分支并安全推送到 `master`（`--force-with-lease`）。
  - 冲突时回滚变基并开启 Issue 告警。
- **选型理由**：最符合“最少新代码，最小维护负担”原则，日常开发无感知。

---

## 4. 详细技术实现 (Technical Specification)

### 4.1 工作流触发与环境
- 文件路径：`.github/workflows/upstream-sync.yml`
- 触发方式：
  - `schedule`: 每天定时触发（如北京时间每日凌晨 04:00）
  - `workflow_dispatch`: 支持随时手动一键触发
- 权限需求：`contents: write`, `issues: write`

### 4.2 Git Rerere 缓存管理
- 远程维护一个孤儿分支（orphan branch）`rerere-cache`，仅存放 `rr-cache.tar.gz`。
- CI 步骤：
  1. 检出仓库完整提交历史（`fetch-depth: 0`）。
  2. 尝试从 `origin/rerere-cache` 检出 `rr-cache.tar.gz` 并解压到 `.git/rr-cache`。
  3. 配置 `git config rerere.enabled true` 和 `git config rerere.autoupdate true`。
  4. 同步完成后，如果产生新的冲突解决记录，自动打包并更新提交至 `rerere-cache`。

### 4.3 变基与冲突处理逻辑
```bash
# 1. 尝试变基
if git rebase upstream/master; then
    # 变基成功且与 origin/master 有差异时推送
    git push --force-with-lease origin master
    # 更新 rerere 缓存
else
    # 变基失败，安全回退
    git rebase --abort
    # 创建/更新 Issue 提示人工介入，附带冲突的提交与文件名
    exit 1
fi
```

### 4.4 历史提交线性化建议
当前 `master` 存在历史 merge commit（如从 2dust/master 的合并 commit）。在首次启用自动 rebase 前，建议本地整理一次，将定制提交以干净的线性方式直接 rebase 在最新的 `upstream/master` 之上。

---

## 5. 待执行计划 (Next Steps)
1. 取得用户对本设计文档的确认。
2. 调用 `superpowers:writing-plans` 编制详细的实施任务列表。
3. 实施 Workflow 与辅助脚本，初始化 `rerere-cache` 分支。
4. 本地测试演练与验证。
