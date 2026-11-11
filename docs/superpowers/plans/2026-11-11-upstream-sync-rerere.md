# 自动化上游同步与 Git Rerere 补丁堆栈 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一套自动同步上游 `2dust/v2rayN:master` 的 GitHub Actions 工作流，利用 Git Rerere 自动重用历史冲突解法，仅在出现无法自动解决的代码冲突时阻断合流并报警。

**Architecture:** 
- 核心同步逻辑封装在轻量级 Bash 脚本 `scripts/upstream-sync.sh` 中，负责 Git Rerere 开启与缓存导入导出、安全变基 `upstream/master` 以及冲突自愈检测。
- 独立的孤儿分支 `rerere-cache` 持久化保存 Git 的 `.git/rr-cache` 数据。
- GitHub Actions 工作流 `.github/workflows/upstream-sync.yml` 按计划调度（Cron）或手动触发（workflow_dispatch），在变基成功时安全推送到 `master` 并更新缓存，变基失败时中止并创建 Issue 报警。

**Tech Stack:** Bash, Git, GitHub Actions CLI / API

**Spec:** `docs/superpowers/specs/2026-11-11-upstream-sync-rerere-design.md`

## Global Constraints
- 必须使用 Git 原生命令实现变基和 Rerere 逻辑，不依赖重型第三方 Action。
- 遇未解冲突时必须立即 `git rebase --abort`，绝不可留存损坏或处于半变基状态的工作区。
- 推送 `master` 时必须使用 `--force-with-lease` 防止覆盖远程新提交。

## Review Focus
1. 上游分支与本地分支完全一致无新提交时：脚本应退出码为 0，且不产生任何多余推送或空提交。
2. 上游更新能够干净 fast-forward 或无冲突 rebase 时：脚本顺利更新分支，不触发冲突处理分支。
3. 遇到冲突且 `.git/rr-cache` 中存在记录时：Rerere 自动解决并顺利提交，成功更新。
4. 遇到冲突且无 Rerere 记录时：变基立刻中止，并返回非零错误码，输出详细的冲突文件和提交信息供 Issue 消费。
5. 首次运行时 `rerere-cache` 远端分支不存在时：脚本正常初始化空缓存，不报错中断。

---

### Task 1: 编写核心同步与 Rerere 管理脚本 `scripts/upstream-sync.sh`

**Files:**
- Create: `scripts/upstream-sync.sh`
- Test: `tests/test-upstream-sync.sh`

**Interfaces:**
- Produces: 
  - `scripts/upstream-sync.sh --restore-rerere <cache_dir>`
  - `scripts/upstream-sync.sh --save-rerere <cache_dir>`
  - `scripts/upstream-sync.sh --sync-rebase <upstream_remote> <upstream_branch>`
  - 退出码 0 代表成功或无变更；退出码 1 代表发生不可自动解决的代码冲突。

- [ ] **Step 1: 编写单元与场景测试脚本 `tests/test-upstream-sync.sh`**

使用临时 Git 仓库模拟：
1. 干净变基场景；
2. 冲突场景（无 rerere，断言失败并回退）；
3. 冲突场景（有 rerere，断言自动解决并成功）。

- [ ] **Step 2: 运行测试以验证失败**

Run: `bash tests/test-upstream-sync.sh`  
Expected: FAIL（脚本尚未实现）

- [ ] **Step 3: 实现 `scripts/upstream-sync.sh`**

包含配置 `rerere.enabled true`、`rerere.autoupdate true`、打包/解包 `rr-cache`，以及执行 `git rebase` 和冲突中止逻辑。

- [ ] **Step 4: 运行测试以验证通过**

Run: `bash tests/test-upstream-sync.sh`  
Expected: PASS（所有模拟场景测试全部通过）

- [ ] **Step 5: 提交代码**

```bash
git add scripts/upstream-sync.sh tests/test-upstream-sync.sh
git commit -m "feat(sync): add upstream sync and rerere management script"
```

---

### Task 2: 配置 GitHub Actions 同步工作流 `.github/workflows/upstream-sync.yml`

**Files:**
- Create: `.github/workflows/upstream-sync.yml`

**Interfaces:**
- Consumes: `scripts/upstream-sync.sh`
- Triggers: `schedule` (cron: '0 20 * * *'), `workflow_dispatch`
- Outputs: 自动更新 `master` 分支，更新 `rerere-cache` 分支，或在冲突时自动创建 Issue。

- [ ] **Step 1: 编写工作流文件 `.github/workflows/upstream-sync.yml`**

包含拉取全量历史、配置 bot 身份、拉取 `upstream`（`https://github.com/2dust/v2rayN.git`）、拉取/推送 `rerere-cache` 孤儿分支，调用 `scripts/upstream-sync.sh`，并在失败时调用 `actions/github-script` 或 gh cli 创建 Issue。

- [ ] **Step 2: 静态校验工作流语法**

通过 bash 或 actionlint/yq 检查 yml 语法正确性。

- [ ] **Step 3: 提交工作流文件**

```bash
git add .github/workflows/upstream-sync.yml
git commit -m "ci(workflow): add automated upstream sync workflow with git rerere"
```

---

### Task 3: 初始化 `rerere-cache` 分支并进行全流程基准整理

**Files:**
- Branch: `rerere-cache` (孤儿分支)
- Documentation: `docs/UPSTREAM_SYNC.md`

- [ ] **Step 1: 编写操作使用文档 `docs/UPSTREAM_SYNC.md`**

详细记录：
1. 日常开发最佳实践（保持本地定制提交整洁）；
2. 遇到冲突 Issue 时的人工处理流程（在本地 rebase，解决冲突并 push，Rerere 自动记录）。

- [ ] **Step 2: 创建并推送空的 `rerere-cache` 孤儿分支准备基线**

本地创建 orphan branch，放入空的占位文件并推送到 origin。

- [ ] **Step 3: 运行完整自动化测试套件确保无回归**

运行项目现有的 smoke test 和 upstream-sync 模拟测试。

- [ ] **Step 4: 提交文档**

```bash
git add docs/UPSTREAM_SYNC.md
git commit -m "docs: add upstream sync and rerere operation guide"
```
