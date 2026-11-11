# 自动化上游同步与 Git Rerere 补丁堆栈指南

本项目通过 GitHub Actions 实现了与上游官方仓库 `2dust/v2rayN:master` 的定时与按需自动同步。

---

## 核心机制概览

```
  上游仓库 2dust/v2rayN:master
             │
             ▼
   [ GitHub Actions 同步任务 ]
             │
             ├─ 1. 从 origin/rerere-cache 分支解压历史冲突解决记录到 .git/rr-cache
             ├─ 2. 执行 git rebase upstream/master
             │
    ┌────────┴────────┐
    │                 │
[无冲突 / Rerere 自动解决]  [出现未曾解决的新代码冲突]
    │                 │
    ├─ 更新 rerere 缓存  ├─ git rebase --abort 安全回滚
    ├─ force-with-lease ├─ 自动创建/更新 GitHub Issue 告警
    │  推送到 master   └─ 流程终止（保证 master 分支完整安全）
    ▼
 同步完成
```

---

## 工作流触发方式

1. **自动定时调度**：每天 UTC 20:00（北京时间次日凌晨 04:00）自动执行。
2. **手动一键同步**：在 GitHub 仓库页面进入 **Actions** -> **Upstream Sync** -> 点击 **Run workflow** 即可手动运行。

---

## 遇代码冲突时的人工处理流程

当出现未曾记录过的代码冲突时，GitHub Actions 会自动在仓库中创建标题为 `[Auto-Sync] Upstream sync conflict detected` 的 Issue。

处理步骤非常简单：

1. **在本地拉取最新上游主干并执行变基**：
   ```bash
   git fetch upstream master
   git checkout master
   git rebase upstream/master
   ```

2. **解决冲突文件**：
   打开冲突文件，保留所需的修改内容后暂存：
   ```bash
   git add <冲突已解决的文件>
   git rebase --continue
   ```
   *说明：由于启用了 `rerere`，Git 会在此时自动记录这次冲突的解决模式（preimage & postimage）。*

3. **安全推送到远程**：
   ```bash
   git push --force-with-lease origin master
   ```

4. **同步更新 Rerere 缓存（可选/推荐）**：
   你可以手动更新 `rerere-cache` 分支，也可以在下次 CI 运行或者手动触发一次 Upstream Sync 时由 CI 自动完成提取与归档。

---

## 本地脚本工具使用

脚本路径：`scripts/upstream-sync.sh`

- **手动执行同步变基**：
  ```bash
  ./scripts/upstream-sync.sh --sync-rebase upstream master
  ```
- **导出本地 Rerere 记录**：
  ```bash
  ./scripts/upstream-sync.sh --save-rerere /path/to/cache_dir
  ```
- **导入 Rerere 记录**：
  ```bash
  ./scripts/upstream-sync.sh --restore-rerere /path/to/cache_dir
  ```
- **运行自动化模拟测试**：
  ```bash
  bash tests/test-upstream-sync.sh
  ```
