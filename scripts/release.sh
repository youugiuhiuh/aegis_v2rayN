#!/usr/bin/env bash
# 一键发版脚本：自动升级版本号，更新 v2rayN/Directory.Build.props，创建 Commit & Tag 并推送远端触发全平台 Release 构建。
#
# 用法：
#   ./scripts/release.sh          # 默认补丁升级 (patch)，如 7.26.5 -> 7.26.6
#   ./scripts/release.sh patch    # 同上
#   ./scripts/release.sh minor    # 次版本升级，如 7.26.5 -> 7.27.0
#   ./scripts/release.sh major    # 主版本升级，如 7.26.5 -> 8.0.0
#   ./scripts/release.sh 7.27.1   # 手动指定目标版本
#
# 可选参数：
#   --no-push                     # 只生成 commit 和 tag，不自动推送远端
#   --no-workflow                 # 仅 push commit & tag，不通过 gh 触发 CI 构建
#   --dry-run                     # 仅预览变更，不修改任何文件
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROPS="$ROOT/v2rayN/Directory.Build.props"

TARGET_ARG=""
PUSH=true
TRIGGER_WORKFLOW=true
DRY_RUN=false

for arg in "$@"; do
  case "$arg" in
    --no-push)
      PUSH=false
      TRIGGER_WORKFLOW=false
      ;;
    --no-workflow)
      TRIGGER_WORKFLOW=false
      ;;
    --dry-run)
      DRY_RUN=true
      ;;
    -h|--help)
      echo "用法: $0 [patch|minor|major|<version>] [--no-push] [--no-workflow] [--dry-run]"
      exit 0
      ;;
    *)
      if [ -z "$TARGET_ARG" ]; then
        TARGET_ARG="$arg"
      else
        echo "❌ 未知参数: $arg" >&2
        exit 1
      fi
      ;;
  esac
done

TARGET_ARG="${TARGET_ARG:-patch}"

# 1. 确保工作区干净
if ! git diff-index --quiet HEAD --; then
  echo "❌ Git 工作区存在未提交的修改，请先提交或 stash 现有改动。" >&2
  git status -s
  exit 1
fi

# 2. 读取当前版本号
if [ ! -f "$PROPS" ]; then
  echo "❌ 找不到版本定义文件: $PROPS" >&2
  exit 1
fi

CURRENT_VERSION="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS" | head -1 | tr -d '[:space:]')"
if [ -z "$CURRENT_VERSION" ]; then
  echo "❌ 无法从 $PROPS 读取当前版本号" >&2
  exit 1
fi

# 3. 计算新版本号
if [[ "$CURRENT_VERSION" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  V_MAJOR="${BASH_REMATCH[1]}"
  V_MINOR="${BASH_REMATCH[2]}"
  V_PATCH="${BASH_REMATCH[3]}"
else
  echo "❌ 当前版本号格式不是标准 semver (x.y.z): $CURRENT_VERSION" >&2
  exit 1
fi

TARGET_ARG="${TARGET_ARG#v}" # 去除开头的 v 前缀

case "$TARGET_ARG" in
  patch)
    NEW_VERSION="${V_MAJOR}.${V_MINOR}.$((V_PATCH + 1))"
    ;;
  minor)
    NEW_VERSION="${V_MAJOR}.$((V_MINOR + 1)).0"
    ;;
  major)
    NEW_VERSION="$((V_MAJOR + 1)).0.0"
    ;;
  *)
    if [[ "$TARGET_ARG" =~ ^[0-9]+\.[0-9]+\.[0-9]+.*$ ]]; then
      NEW_VERSION="$TARGET_ARG"
    else
      echo "❌ 非法的目标版本: $TARGET_ARG (应为 patch, minor, major 或具体版本如 7.26.6)" >&2
      exit 1
    fi
    ;;
esac

if [ "$CURRENT_VERSION" = "$NEW_VERSION" ]; then
  echo "⚠️ 当前版本已是 $NEW_VERSION，无需更新。"
  exit 0
fi

echo "🚀 发版升级: $CURRENT_VERSION -> $NEW_VERSION"

if [ "$DRY_RUN" = true ]; then
  echo "（dry-run 模式：仅预览，不执行修改）"
  exit 0
fi

# 4. 修改 Directory.Build.props
echo ">>> 更新 $PROPS"
sed -i "s/<Version>.*<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$PROPS"

# 5. Git 提交
CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
echo ">>> 创建 Git Commit: chore(release): bump version to $NEW_VERSION"
git add "$PROPS"
git commit -m "chore(release): bump version to $NEW_VERSION"

# 6. Git 标签
echo ">>> 创建 Git Tag: $NEW_VERSION"
git tag -a "$NEW_VERSION" -m "Release $NEW_VERSION"

# 7. 推送远端
if [ "$PUSH" = true ]; then
  echo ">>> 推送至 origin $CURRENT_BRANCH 及 tag $NEW_VERSION"
  git push origin "$CURRENT_BRANCH"
  git push origin "$NEW_VERSION"
  echo "🎉 发布提交与标签已成功推送！"

  # 8. 触发 GitHub Actions 全平台 Release 构建
  if [ "$TRIGGER_WORKFLOW" = true ]; then
    if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
      echo ">>> 正在通过 gh CLI 触发 build-all.yml (release all platforms)..."
      REPO_SLUG="$(git remote get-url origin 2>/dev/null | sed -E 's/.*github\.com[:\/](.*)\.git/\1/')"
      REPO_FLAG=()
      if [ -n "$REPO_SLUG" ]; then
        REPO_FLAG=(--repo "$REPO_SLUG")
      fi
      if gh workflow run build-all.yml "${REPO_FLAG[@]}" --ref "$CURRENT_BRANCH" -f release_tag="$NEW_VERSION"; then
        echo "✅ 成功触发全平台构建流水线：https://github.com/${REPO_SLUG}/actions/workflows/build-all.yml"
      else
        echo "⚠️ 触发 workflow 失败，可手动在 GitHub Actions 页面触发 build-all.yml。"
      fi
    else
      echo "💡 gh CLI 未安装或未登录，请在 GitHub Actions 页面手动运行 'release all platforms' 工作流。"
    fi
  fi
else
  echo "✅ 本地 Commit & Tag 完成（已指定 --no-push，未推送远端）。"
fi
