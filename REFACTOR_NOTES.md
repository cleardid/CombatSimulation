# 重构说明

本次重构以“减少重复样式、保留现有业务逻辑、便于后续维护”为目标。

## 1. 弹窗统一父类

所有编辑弹窗已改为继承 `Views/Dialogs/DialogWindowBase.cs`。

父类只做一件事：监听弹窗 ViewModel 的 `CloseRequested` 事件，并统一设置 `DialogResult`。这样每个弹窗代码后置文件不再重复写 `OnCloseRequested`。

## 2. 弹窗统一样式

弹窗公共样式集中在：

`Resources/Styles/DialogWindowStyles.xaml`

主要包含：

- `DialogWindowStyle`：弹窗窗口背景、默认前景色等；
- `DialogShellBorderStyle`：弹窗内容外层面板；
- `DialogLabelStyle` / `DialogColonStyle`：表单标签与冒号；
- `DialogTextBoxStyle` / `DialogDescriptionTextBoxStyle` / `NumericTextBoxStyle`：输入框；
- `DialogComboBoxStyle` / `DialogComboBoxItemStyle`：下拉框；
- `ReadOnlyDisplayBoxStyle` / `ReadOnlyDisplayTextStyle`：只读展示字段；
- `DialogButtonStyle`：保存、取消按钮；
- `DialogValidationTextStyle`：校验提示。

后续如需修改弹窗颜色，优先修改该文件中的 Brush，不建议在某个弹窗 XAML 内单独覆盖颜色。

## 3. 文件夹规整

ViewModel 已按功能拆入：

- `ViewModels/Common`
- `ViewModels/Main`
- `ViewModels/Deduction`
- `ViewModels/Targets`
- `ViewModels/Dialogs/Targets`
- `ViewModels/Dialogs/DamageTrees`

Model 已按功能拆入：

- `Models/Targets`
- `Models/DamageTrees`
- `Models/Deduction`
- 原有 `Models/Unity` 保持不变。

弹窗 View 移入：

- `Views/Dialogs`

注意：为减少 XAML 与 using 的连锁修改，命名空间保持原有 `CombatSimulation.ViewModels`、`CombatSimulation.Models`、`CombatSimulation.Views`。文件夹变化不会影响编译。

## 4. 资源文件归并

原 `Resource` 文件夹已删除，按钮图片和弹窗背景图片统一迁移到：

`Resources/Images`

当前图片资源通过 `CombatSimulation.csproj` 中的 `Resource Include="Resources\Images\*.png"` 统一编译为 WPF Resource。

图片引用路径已同步改为：

- `/Resources/Images/btnAdd.png`
- `/Resources/Images/btnAdd_L.png`
- `/Resources/Images/btnDel.png`
- `/Resources/Images/btnDel_L.png`
- `/Resources/Images/btnMod.png`
- `/Resources/Images/btnMod_L.png`
- `pack://application:,,,/Resources/Images/pop.png`

## 5. 后续修改建议

1. 新增弹窗时，XAML 根节点使用 `dialogs:DialogWindowBase`，并设置 `Style="{StaticResource DialogWindowStyle}"`。
2. 新增弹窗 ViewModel 时实现 `IDialogRequestClose`，在保存或取消时触发 `CloseRequested`。
3. 新弹窗中不要重复声明输入框、按钮、下拉框样式，直接引用共享样式键。
4. 页面级特殊样式可以保留在页面 XAML 内，但公共样式应放入 `Resources/Styles`。

## 2026-05-16 弹窗样式运行期异常修复

`WindowStartupLocation` 不是 WPF 依赖属性，不能放入 `Style` 的 `Setter` 中，否则加载 `DialogWindowStyles.xaml` 时会抛出：

`System.Windows.Markup.XamlParseException: 设置属性 System.Windows.Setter.Property 时引发了异常。ArgumentNullException: Value cannot be null. (Parameter 'property')`

处理方式：

- 已从 `Resources/Styles/DialogWindowStyles.xaml` 的 `DialogWindowStyle` 中移除 `WindowStartupLocation` Setter。
- 已在 `Views/Dialogs/DialogWindowBase.cs` 构造函数中统一设置 `WindowStartupLocation = WindowStartupLocation.CenterOwner;`。
- 单个弹窗如需特殊位置，仍可在对应 XAML 根节点上显式设置 `WindowStartupLocation`。
