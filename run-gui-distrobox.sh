#!/usr/bin/env bash
# 一键使用 Distrobox / Podman 运行与测试 aegis_v2rayN GUI 桌面端
#
# 用法：
#   ./run-gui-distrobox.sh          # 直接在 Distrobox 容器中启动 v2rayN GUI 界面
#   ./run-gui-distrobox.sh --test   # 运行无头 (Xvfb) 冒烟健康自检，验证 UI 初始化与无崩溃
#   ./run-gui-distrobox.sh --export # 将容器内的 v2rayn 导出为宿主机桌面图标与 ~/.local/bin/v2rayn
#   ./run-gui-distrobox.sh --rebuild# 强制重新编译发布 Linux 桌面端再启动
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
BOX_NAME="aegis-v2rayn-box"
BOX_IMAGE="ubuntu:24.04"
PUBLISH_DIR="$ROOT/publish/linux-desktop"
BINARY="$PUBLISH_DIR/v2rayN"

# 1. 检查 distrobox
if ! command -v distrobox >/dev/null 2>&1; then
  echo "❌ 未检测到 distrobox。请先安装: sudo apt install distrobox 或 pacman -S distrobox" >&2
  exit 1
fi

# 2. 检查并创建 distrobox 容器
if ! distrobox list 2>/dev/null | grep -qw "$BOX_NAME"; then
  echo "📦 正在创建 Distrobox 隔离容器: $BOX_NAME ($BOX_IMAGE)..."
  distrobox create --name "$BOX_NAME" --image "$BOX_IMAGE" \
    --additional-flags "--cap-add=NET_ADMIN --device=/dev/net/tun" \
    --yes
fi

# 3. 检查容器内 GUI 运行时依赖
echo "🔍 检查并就绪容器内 GUI 运行依赖 (X11 / Fonts / CJK)..."
distrobox enter "$BOX_NAME" -- bash -c '
if ! dpkg -s libfontconfig1 fonts-noto-cjk libice6 libsm6 libx11-6 libxext6 libxi6 libxrender1 libxtst6 libgl1 libegl1 libicu74 xvfb >/dev/null 2>&1; then
  echo "⚙️ 正在安装容器内 GUI 与中文字体依赖..."
  sudo apt-get update -qq
  sudo apt-get install -y -qq \
    libfontconfig1 fonts-noto-cjk libice6 libsm6 libx11-6 libxext6 \
    libxi6 libxrender1 libxtst6 libgl1 libegl1 libicu74 xvfb >/dev/null 2>&1
fi
'

# 4. 检查是否需要编译
REBUILD=false
MODE="run"

for arg in "$@"; do
  case "$arg" in
    --rebuild)
      REBUILD=true
      ;;
    --test)
      MODE="test"
      ;;
    --export)
      MODE="export"
      ;;
    -h|--help)
      echo "用法: $0 [--test|--export|--rebuild]"
      exit 0
      ;;
  esac
done

if [ "$REBUILD" = true ] || [ ! -f "$BINARY" ]; then
  echo "🔨 正在编译发布 aegis_v2rayN Desktop (linux-x64)..."
  if command -v mise >/dev/null 2>&1; then
    (cd "$ROOT" && mise x -- dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 --self-contained -o "$PUBLISH_DIR")
  else
    (cd "$ROOT" && dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 --self-contained -o "$PUBLISH_DIR")
  fi
  chmod +x "$BINARY"
fi

# 5. 执行模式
case "$MODE" in
  test)
    echo "🧪 正在 Distrobox 容器内执行 Xvfb 无头冒烟健康自检..."
    distrobox enter "$BOX_NAME" -- bash -c "
      cd '$PUBLISH_DIR'
      echo '启动进程进行 4 秒运行探测...'
      xvfb-run -a timeout --preserve-status 4 ./v2rayN || code=\$?
      if [ \"\${code:-0}\" -eq 124 ] || [ \"\${code:-0}\" -eq 143 ] || [ \"\${code:-0}\" -eq 0 ]; then
        echo '✅ 冒烟自检通过！v2rayN 成功启动且未发生任何 XAML/Avalonia 运行时异常。'
        exit 0
      else
        echo '❌ 冒烟测试失败，进程异常退出，错误码: ' \${code:-unknown}
        exit 1
      fi
    "
    ;;

  export)
    echo "📤 正在导出 v2rayN 到宿主机桌面菜單與 PATH..."
    distrobox enter "$BOX_NAME" -- bash -c "
      distrobox-export --bin '$BINARY' --export-path ~/.local/bin
    "
    echo "🎉 導出完成！現在您可以在宿主機任意終端直接運行 'v2rayN'，或在應用菜單中點擊打開。"
    ;;

  run)
    echo "🚀 正在 Distrobox 容器中啟動 v2rayN GUI (無縫對接宿主機顯示環境)..."
    distrobox enter "$BOX_NAME" -- bash -c "
      cd '$PUBLISH_DIR'
      ./v2rayN \"\$@\"
    "
    ;;
esac
