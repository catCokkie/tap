# 验证记录

- 日期：2026-02-16
- 执行者：Codex
- 目标：V0.2 Day 1 基线验证

## 1. 构建验证

- 命令：`dotnet build tap.csproj -v minimal`
- 结果：成功
- 摘要：
  - 0 warning
  - 0 error
  - 输出：`.godot/mono/temp/bin/Debug/tap.dll`

## 2. 文档一致性验证

- 已修复 `game-planning/README.md` 中失效的“完整企划书”链接。
- 已修复 `game-planning/TODO.md` 中失效引用与过期任务状态。
- 已同步 `game-planning/GameDesign.md` 的关键数值到当前代码口径：
  - 首个突破需求：100 CP。

## 3. 当前结论

- 当前代码可稳定构建。
- Day 1 文档与基线数值对齐完成。
- 下一步进入 V0.2 Day 2（数据驱动改造）。

---

## 4. 输入系统阶段验证（2026-02-16）

- 变更范围：
  - `Scripts/Game/InputScoringSystem.cs`（新增）
  - `Scripts/Core/GameManager.cs`（接入输入系统）
  - `Scripts/Core/GameState.cs`（新增输入参数与统计字段）
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 5. 输入驱动替代离线路径验证（2026-02-16）

- 变更范围：
  - `Scripts/Core/GameManager.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 已关闭离线修为发放逻辑。
  - 启动时仅提示离开时长并恢复在线输入驱动。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 6. 输入分配策略后端验证（2026-02-16）

- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Game/InputScoringSystem.cs`
  - `Scripts/Core/GameManager.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 筑基前强制 100% 主修炼。
  - 筑基后默认自动分配。
  - 已支持手动权重字段与归一化（后续补UI切换）。
  - 非主修炼分配先进入各阶段资源池（灵药/灵宠/炼丹/炼器）。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 7. 手动分配UI接入验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 设置按钮已接入“输入分配设置”弹窗。
  - 支持自动/手动切换、五项权重滑条、恢复默认、保存并应用。
  - 筑基前禁用手动分配，筑基后自动解锁。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 8. 转世继承输入加成验证（2026-02-16）

- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Game/InputScoringSystem.cs`
  - `Scripts/Core/GameManager.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增转世判定与执行逻辑（分神圆满后再次突破触发转世）。
  - 输入上限与输入转化率改为读取“转世加成后”有效值。
  - 启动时和转世后会记录当前转世加成信息。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 9. 转世预览UI验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/GameHUD.cs`
- 说明：
  - 设置面板新增“转世预览”区块。
  - 可查看当前轮回次数、当前输入上限/转化率、下一次转世预期加成。
  - 可查看当前是否满足转世条件。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 10. 调试面板接入验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/GameHUD.cs`
  - `Scenes/Main.tscn`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 主界面新增“调试”按钮，打开运行时调试面板。
  - 支持：加修为、改境界、资源池注入、被动修炼开关、模拟/触发转世、保存并重载。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 11. 灵药系统v1后端验证（2026-02-16）

- 变更范围：
  - `Scripts/Game/HerbGardenSystem.cs`（新增）
  - `Scripts/Core/GameState.cs`
  - `Scripts/Core/GameManager.cs`
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 已接入灵药园自动生长与自动收获流程（消耗 `HerbGardenPool`）。
  - 已实现基础药材入库与稀有掉落（元婴后）。
  - 调试面板新增灵药相关测试指令（进度置满、库存注入、库存查看）。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 12. 灵药系统可视化验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/HerbGardenDisplay.cs`（新增）
  - `Scenes/Main.tscn`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 主界面新增灵药园面板，显示解锁状态、2个药槽进度、药材库存摘要。
  - 与 `HerbGardenPool`、药槽状态、库存状态实时联动。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 13. 灵药策略切换验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/GameHUD.cs`
  - `Scripts/Game/HerbGardenSystem.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 设置面板新增灵药策略下拉（均衡种植 / 单药冲刺）。
  - 保存后写入 `GameState.ActiveHerbStrategy`。
  - 灵药后端每轮按策略刷新药槽目标并执行生长。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 14. 统一库存底座验证（2026-02-16）

- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Game/HerbGardenSystem.cs`
  - `Scripts/UI/HerbGardenDisplay.cs`
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/systems/库存系统设计.md`
  - `game-planning/systems/辅助系统设计索引.md`
  - `game-planning/GameDesign.md`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增通用库存结构 `Inventory` 与统一读写接口。
  - 灵药系统与相关UI已切换到统一库存接口。
  - 新增库存系统设计文档并接入总设计索引。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 15. 炼丹房v1后端验证（2026-02-16）

- 变更范围：
  - `Scripts/Game/AlchemySystem.cs`（新增）
  - `Scripts/Core/GameState.cs`
  - `Scripts/Core/GameManager.cs`
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 元婴后自动处理炼丹进度（消耗 `AlchemyPool`）。
  - 支持丹方：`ningqi_pill_recipe`、`pojing_pill_recipe`。
  - 自动消耗灵药库存并产出丹药库存（分类 `pill`）。
  - 调试面板新增：炼丹材料注入、丹方切换、丹药库存查看。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 16. 炼丹房可视化验证（2026-02-16）

- 变更范围：
  - `Scripts/UI/AlchemyDisplay.cs`（新增）
  - `Scenes/Main.tscn`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 主界面新增炼丹房展示区块。
  - 可查看：解锁状态、当前丹方、炼丹进度、材料库存、丹药库存。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 17. 凝气丹效果消费验证（2026-02-16）
- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Core/GameManager.cs`
  - `Scripts/Game/AlchemySystem.cs`
  - `Scripts/UI/AlchemyDisplay.cs`
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增凝气丹限时增益字段与消耗接口（提升输入转化率）。
  - `GameManager._Process` 接入限时效果倒计时。
  - 炼丹系统支持“自动服用凝气丹”逻辑（增益结束后自动续上）。
  - 炼丹界面与调试面板增加增益状态、自动服用状态与手动服用调试能力。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 18. 破境丹效果消费验证（2026-02-16）
- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Game/AlchemySystem.cs`
  - `Scripts/UI/AlchemyDisplay.cs`
  - `Scripts/UI/GameHUD.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增破境丹限时增益字段与消耗接口（突破需求降低）。
  - `GetBreakthroughRequirement()` 接入破境丹增益效果。
  - 炼丹系统支持“自动服用破境丹”逻辑（增益结束后自动续上）。
  - 炼丹界面与调试面板增加破境丹增益状态与手动/自动调试能力。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 19. 破境丹省药模式验证（2026-02-16）
- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Game/AlchemySystem.cs`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增 `ShouldAutoUsePojingPillForBreakthrough()` 判定。
  - 自动服用破境丹改为“省药模式”：仅在当前修为达到“减免后门槛”且未达到“原门槛”时触发。
  - 同步修复 `GameState` 中少量注释与属性挤在同一行导致的语法问题。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 20. 灵宠园 v1 验证（2026-02-16）
- 变更范围：
  - `Scripts/Core/GameState.cs`
  - `Scripts/Core/GameManager.cs`
  - `Scripts/Game/SpiritPetSystem.cs`（新增）
  - `Scripts/Game/HerbGardenSystem.cs`
  - `Scripts/UI/SpiritPetDisplay.cs`（新增）
  - `Scripts/UI/GameHUD.cs`
  - `Scenes/Main.tscn`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增灵宠园后端循环：消耗 `SpiritPetPool` 推进捕捉进度，自动产出灵宠；容量满时转化为 `pet_essence`。
  - 新增灵宠状态与加成计算，接入输入上限、输入转化率、灵药成长效率。
  - 新增灵宠园展示面板（解锁状态、进度、总加成、灵宠列表）。
  - 重建并稳定化 `GameHUD`，保留设置面板与调试面板能力，并新增灵宠调试操作。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 21. Windows 全局输入采集验证（2026-02-16）
- 变更范围：
  - `Scripts/Platform/WindowsGlobalInputListener.cs`（新增）
  - `Scripts/Game/InputScoringSystem.cs`
  - `game-planning/GameDesign.md`
  - `game-planning/ExecutionPlan.md`
- 说明：
  - 新增 Windows 全局键鼠 Hook 监听器（键盘按下、左右键按下、滚轮）。
  - 输入计分系统改为优先消费全局输入队列，支持失焦后台运行。
  - 当全局 Hook 启用时，关闭前台 `_Input` 计分路径，避免双重计分。
  - 当全局 Hook 不可用时，自动回退到原前台输入模式。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 22. 输入统计页面验证（2026-02-16）
- 变更范围：
  - `Scripts/UI/GameHUD.cs`
- 说明：
  - 新增“输入统计”按钮与弹窗页面（简约版）。
  - 支持三轮实验选择（低/中/高）、开始本轮、结束本轮、重置三轮。
  - 实时显示每分钟指标：Raw/Eff 输入点、主修炼、灵药池/灵宠池/炼丹池分配速率。
  - 汇总显示三轮结果，便于基线对比。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）

## 23. 三菜单导航页验证（2026-02-16）
- 变更范围：
  - `Scripts/UI/GameHUD.cs`
- 说明：
  - 新增顶部三菜单入口：功能菜单 / 游戏菜单 / 测试菜单。
  - 功能菜单：灵药园概览、灵宠园概览、背包弹窗。
  - 游戏菜单：输入分配设置、保存游戏、重置游戏。
  - 测试菜单：输入统计页、调试面板。
  - 移除对 F8/F9 快捷键入口的依赖，改为可视化菜单入口。
- 验证命令：`dotnet build tap.csproj -v minimal`
- 结果：成功（0 warning / 0 error）
