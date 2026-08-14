# DeepSeek Harness 桌面客户端

> **版本 v1.0.0** · 作者 **Muxue-Dev（幻曦之殇）**
> GitHub：https://github.com/Muxue-Dev ｜ B站：https://space.bilibili.com/110628804

## 📥 下载安装包（永久免费）

**夸克网盘下载（推荐，不限次数）：**
- 链接：https://pan.quark.cn/s/37a8b3710a3d
- 提取码：`87Ft`

**安装方法**：下载后右键解压 → 双击里面的「安装.exe」→ 装好即用（桌面自动出快捷方式）。

> ⚠️ 本软件**永久免费**。若你在别处被要求**付费**才能下载，那是被人倒卖了，请认准本仓库免费获取。

## ✨ 这是什么

把 DeepSeek Harness（DSH，网页版 AI 助手）封装成一个独立的 Windows 桌面软件：WebView2 内嵌窗口 + 内置 Node，双击就能用，适合完全不懂电脑的小白用户。

## ✨ 主要功能

- 真·应用窗口：用 Edge WebView2 把网页内嵌进程序窗口（无地址栏、无标签页）
- 内置独立 Node.js（runtime\node），不依赖 WorkBuddy
- 复用后台：打开/关闭客户端都不杀后台，不影响正在进行的对话
- 软件图标 + 加载页 + 「关于」页（版本 / GitHub / B站链接）

## 🎨 皮肤（可选 · 免费）

软件默认是官方界面。想要「深海女仆工坊」（双女仆背景）皮肤，可自行安装：

- 皮肤项目：[dsh-deep-whale](https://github.com/Small-tailqwq/dsh-deep-whale)（作者 Small-tailqwq，免费）
- 注！此项目含有以 @上善 创作的鲸鱼娘为基础的二次创作，CC 协议，**禁止商用**。

**安装方法**：装好客户端后，把你的 AI 助手喊来，把这个皮肤项目链接发它，让它帮你装即可（或到皮肤项目页看说明）。

## 📖 怎么用

1. 下载安装包，解压，双击「安装.exe」
2. 双击桌面「DeepSeek Harness 客户端」
3. 想退出：点窗口右上角 ×（只关窗口，后台服务继续运行）

## 🔧 从源码编译

- 环境：Windows + .NET Framework 4.x（系统自带 csc.exe）+ WebView2 SDK DLL
- 方法：运行 `源码\build.bat` 一键重新编译生成 exe

## 📂 目录结构
DeepSeek-Harness-Client.exe 主程序 Microsoft.Web.WebView2.*.dll 内嵌浏览器（WebView2） WebView2Loader.dll 内嵌浏览器加载器 runtime\node\node.exe 内置独立 Node.js config.txt 配置（路径都在这里改） 源码\launcher.cs 客户端源码（C#） 源码\build.bat 一键编译脚本

## 📄 开源协议

- 客户端源码：MIT License
- 「深海女仆工坊」皮肤为第三方作品（CC BY-NC-SA），不随本仓库分发
