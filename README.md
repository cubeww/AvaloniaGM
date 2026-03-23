# AvaloniaGM

**AvaloniaGM**是一个开源的 **GameMaker Studio 1.4** 项目编辑器，使用 C# + AvaloniaUI 构建，目标是提供更好的编辑体验。

## 文件夹

- AvaloniaGM：编辑器源码
- AvaloniaGM.Verifier：控制台验证项目，用于手动验证序列化、图片导出等效果
- TestProjects：测试用GameMaker Studio 1.4项目
- GMAssetCompiler：官方的GameMaker项目解析/编译器，仅供参考

## 项目结构

- Models：模型层，存放各种GameMaker资源的定义
  - Project：GameMaker项目整体，包含各种类型的Resource

  - Resource：GameMaker资源基类

  - Sprite：Sprite资源

  - ... 其它资源

- Services：服务层
  - ProjectGmxSerializer：GameMaker Studio 1.4 项目序列化/反序列器，负责从.project.gmx反序列化到Project，以及从Project序列化到.project.gmx（以及附属文件）

## 当前状态

- `ProjectGmxSerializer` 已支持项目级反序列化与序列化
- 当前已接入的主要资源模型：
  - `Project`
  - `Sprite`
  - `Background`
  - `Sound`
  - `GamePath`
  - `Font`
  - `GameObject`
  - `Room`
  - `Timeline`
  - `DataFile`
  - `Extension`
  - `Script`
  - `Shader`
- 当前项目级序列化已覆盖：
  - `.project.gmx`
  - `help.rtf`
  - `Configs` 配置文件树
  - `datafiles`
  - `extensions` 的 `gmx`、包体文件、included resources
  - `sound/audio`
  - `sprites/images`
  - `background/images`
  - `fonts/*.png`
  - 各资源对应的 `*.gmx` 或源码文件
- `SpriteFrame.Bitmap`、`Background.Bitmap`、`Font.Bitmap` 使用 Avalonia 可直接消费的位图对象，当前图片导出仍走 `Bitmap` 路线
- `Shader` 的项目级 `type` 属性已保留，当前测试项目中的 `GLSLES` 可正确 round-trip
- `datafiles` 节点的 `number` 已按当前测试项目的 GMX 实际表现回写

## 验证方式

- 目前不再维护单元测试项目
- 需要验证某些效果时，统一在 `AvaloniaGM.Verifier` 中编写临时代码并运行
- 涉及 Avalonia 位图导出、项目回写这类效果时，优先使用 `AvaloniaGM.Verifier`，不要依赖 headless 测试环境

## 当前注意事项

- 目标是让“序列化到新位置后与原项目内容基本一致”，但不强求逐字节一致
- 目前常见残余差异主要是：
  - XML 属性顺序
  - 自闭合标签写法
  - 重新编码后的图片文件体积变化
- 原项目中如果存在未挂到 `.project.gmx` 资源树里的孤儿文件，当前不会主动把它们补回项目树

## 模型开发原则

- `GMAssetCompiler` 仅作为参考，不直接照搬其完整字段定义
- 由于 `GMAssetCompiler` 版本较新，可能包含高版本 GameMaker 的字段，因此模型层应优先以对应资源在 `GMAssetCompiler` 中的 **GMX 构造函数实际解析的字段** 为准
- 测试项目中的 `*.gmx` 文件用于验证这些字段是否真实出现在当前要支持的 GameMaker Studio 1.4 项目中
- 模型层应尽量保持纯净，只保留当前编辑器直接需要的资源数据，不在模型中保存路径等过程性信息
- 如果为了编辑器预览必须额外保留 UI 可直接消费的数据，可以少量增加这类字段；当前典型例子是 `SpriteFrame.Bitmap` 和 `Background.Bitmap`


