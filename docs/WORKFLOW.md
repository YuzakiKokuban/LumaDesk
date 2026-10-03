# 开发与发布流程

`dev` 用于日常开发，`main` 保存经过验证的交付代码。两个长期分支之间使用 Rebase and merge，保持线性历史。

## 日常开发

- 小改动直接提交到 `dev`；较大改动从 `dev` 创建 `codex/<功能名>`，通过 PR 合回。
- 推送到 `main`、`dev` 以及目标为这两个分支的 PR 均执行 CI，生成可下载的测试包。普通提交不创建 Release。
- Dependabot 的依赖升级 PR 以 `dev` 为目标。其配置需要进入默认分支后才能被服务读取。
- 硬件功能修改除 CI 外，还需完成相应实机验证，并更新验证记录。

## dev 合入 main

发布期间暂停向 `dev` 写入，确认工作区干净，然后执行：

```powershell
git fetch origin
git switch dev
git pull --ff-only origin dev
$expectedDev = git rev-parse origin/dev
git rebase origin/main
# 如有冲突，解决后 git add，再 git rebase --continue。
# 完成相关本地验证后推送；远程有新提交时租约检查会拒绝覆盖。
git push "--force-with-lease=refs/heads/dev:$expectedDev" origin dev
```

创建 `dev → main` PR，等待最新 CI 通过，使用 **Rebase and merge**。`main` 要求 PR、Windows x64 检查通过、线性历史，并禁止强推和删除；不要求他人审批。

合并前记录 PR 最终源提交 SHA。GitHub rebase 合并会重写 SHA；合并后必须对齐 `dev`。确认 PR 已合并、工作区干净，并且本地和远程 `dev` 都仍等于记录的源提交。若出现新提交，先保留分支，再将新提交 rebase 到 `origin/main`，不要执行覆盖操作。

确认没有新增提交后，将占位符替换为记录的 SHA：

```powershell
$mergedDev = '<PR 最终源提交 SHA>'
git fetch origin
git switch dev
if (git status --porcelain) { throw '工作区不干净' }
if ((git rev-parse HEAD) -ne $mergedDev) { throw '本地 dev 已变化，停止对齐' }
if ((git rev-parse origin/dev) -ne $mergedDev) { throw '远程 dev 已变化，停止对齐' }
git branch "backup/dev-$($mergedDev.Substring(0, 12))" dev
if ($LASTEXITCODE -ne 0) { throw '备份失败，停止对齐' }
git reset --hard origin/main
git push "--force-with-lease=refs/heads/dev:$mergedDev" origin dev
```

`dev` 的强推仅用于维护者执行 rebase 或合并后对齐。不要在其他人正在写入时改写历史。

## 版本发布

- 测试版：在已验证且属于 `dev` 的提交上创建 `v<版本>-beta.<序号>` 标签。
- 正式版：在已合入 `main` 的提交上创建 `v<版本>` 标签。
- 标签必须与 `Cargo.toml` 版本一致，前后端版本由构建脚本检查；发布说明位于 `docs/releases/<版本>.md`。
- 标签触发完整 CI。发布前检查标签提交与检出提交一致，并验证其属于对应远程分支；通过后发布 ZIP、安装包及 SHA256。
- 已发布标签不移动、不复用。修复后递增版本，创建新标签。

紧急修复从 `main` 建立短期分支，经 PR 和 CI 后 rebase 合入 `main`，发布补丁版本；随后将 `dev` rebase 到最新 `main` 并重新验证。
