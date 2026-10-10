# Frontend Directory Structure

> 项目：视频格式转换器（WinUI 3）| 更新：2026-10-10

```
src/VideoConverter/
  App.xaml(.cs)                   # 应用入口；App.MainWindow 静态属性供文件选择器取窗口句柄
  MainWindow.xaml(.cs)            # 单页三列布局；代码后置只处理需要 hwnd 的选择器与拖放
  ViewModels/MainViewModel.cs     # 唯一 ViewModel
  Helpers/                        # 值转换器等（当前为空：改用 VM 暴露 Brush/Visibility）
  app.manifest                    # 申明 supportedOS
```

规则：

- XAML 里不写业务逻辑；代码后置只允许：文件/保存选择器、拖拽事件、窗口尺寸配置
- 新增页面先考虑能否并入现有单页，MVP 不引入导航框架
