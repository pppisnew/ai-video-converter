# Backend Error Handling

> 项目：视频格式转换器 | 更新：2026-10-10

## 约定

- Core 服务对"业务失败"返回 `ConversionResult`（Success/ExitCode/Error），不抛异常
- 对"调用方错误"抛标准异常：`FileNotFoundException`（输入不存在）、
  `InvalidOperationException`（外部进程异常，消息含退出码 + stderr 摘要）
- UI 层统一捕获并在状态栏展示，错误用 `StatusIsError` + 红色画笔区分

## 部分产物清理

转换失败或被取消时**必须删除输出文件**。半成品 MP4 可以播放前几秒，
用户会误认为是 bug。删除失败（文件被占用）时静默忽略，不要向上抛。

## 异常链

进程启动失败要包成 `InvalidOperationException` 并保留 inner exception，
不要吞掉 `Win32Exception`（找不到可执行文件时它会带真正的错误码）。
