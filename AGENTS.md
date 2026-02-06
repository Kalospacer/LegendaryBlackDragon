# agent.md - LegendaryBlackDragon 协作规范

## 1) 工作原则（最高优先级）

### 原版优先（Best Practice）
- 实现前先查 RimWorld 1.6 原版源码与调用链。
- 优先复用原版机制（Job/Toil/Verb/Comp/Effecter），避免重复造轮子。
- 仅在原版无法满足需求时做最小扩展。

### 精简实现（more is less, less is good）
- 小改动、低耦合、少状态、少分支。
- 优先删冗余逻辑，不叠补丁式代码。
- 优先参数化，不新增复杂系统。
- 保持可读、可维护、可回退。

## 2) RimWorld 开发强制规则

### 知识库使用
- 进行 RimWorld Mod 开发时，必须优先使用 `rimworld-code-rag`（MCP）检索：
  - 类名、方法签名、枚举值
  - 原版机制与行为
  - 反编译 1.6 源码
- 不依赖记忆臆断原版实现。

### 关键路径
- 原版源码库：`C:\Steam\steamapps\common\RimWorld\dll1.6`
- 本项目根目录：`C:\Steam\steamapps\common\RimWorld\Mods\LegendaryBlackDragon`
- C# 工程目录：`C:\Steam\steamapps\common\RimWorld\Mods\LegendaryBlackDragon\Source\LegendaryBlackDragon`

## 3) 构建与输出

### 推荐构建命令（VS2022 MSBuild）
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" LegendaryBlackDragon.csproj -p:Configuration=Release -verbosity:minimal
```

### 输出目录
- `C:\Steam\steamapps\common\RimWorld\Mods\LegendaryBlackDragon\1.6\1.6\Assemblies\`

## 4) 代码风格

- 命名：类/方法/属性 PascalCase；字段 camelCase。
- 缩进：4 空格；大括号换行。
- C#：优先清晰可读；明显类型可用 `var`。
- 序列化：`Scribe_Values.Look()` / `Scribe_Collections.Look()`。

## 5) 本地化与文案

### Def 文本
- Def 的 `label/description` 使用 `DefInjected` 本地化。

### C# 文本（后续统一）
- C# 字符串统一走 `Languages/.../Keyed/*.xml`。
- 新增 C# 文案时，不硬编码中文/英文，直接使用翻译 Key。

## 6) 实施流程（每次改动）

1. 先检索原版对应实现（rimworld-code-rag）。
2. 选原版最接近模式并最小实现。
3. 参数化暴露到 XML（避免硬编码魔法数）。
4. 构建验证并确认 DLL 输出路径正确。
