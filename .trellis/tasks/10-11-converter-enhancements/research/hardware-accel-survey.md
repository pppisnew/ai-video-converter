# 硬件加速编码器可用性调研

调研时间：2026-10-11 | 方式：本机 ffmpeg 9.0.1 实测

## 本机环境

- GPU：NVIDIA GeForce RTX 4050 Laptop GPU，驱动 591.74
- 无 Intel 集显输出（Win32_VideoController 仅列出 NVIDIA 与一个虚拟显示器）
- 无 AMD GPU

## ffmpeg 编码器列表 vs 实际可用性（关键结论）

捆绑的 ffmpeg n9.0.1-11 列出了全部三家的硬件编码器，但**实测全部不可用**：

| 编码器 | `-encoders` 列表 | 真实试编码 | 失败原因 |
|---|---|---|---|
| h264_nvenc | 有 | 失败 | Driver does not support the required nvenc API version. Required: 13.1 Found: 13.0（需驱动 610+） |
| hevc_nvenc | 有 | 失败 | 同上 |
| h264_qsv | 有 | 失败 | Error creating a MFX session: -9（无 Intel GPU） |
| h264_amf | 有 | 失败 | DLL amfrt64.dll failed to open（无 AMD GPU） |
| libx264 / libx265 / libvpx-vp9 | 有 | **可用** | - |

**核心结论：只查 `-encoders` 列表完全不足以判断硬件加速可用性。**
编码器在列表里存在，但可能因驱动版本过旧、GPU 不在位、运行时 DLL 缺失而初始化失败。
因此探测必须执行一次真实试编码，以退出码为准。

## 探测方法（已定）

```bash
ffmpeg -hide_banner -loglevel error -y -f lavfi -i testsrc=duration=1:size=320x240:rate=1 -c:v <encoder> -f null -
```

退出码为 0 才算可用。1 秒 320x240 的微型片源，单次探测耗时 < 1s。

## 编码器 × 编码格式组合矩阵

硬件编码器按"编码格式"独立存在，必须分别探测，不能探一个推断另一个：

| 视频编码 | NVIDIA | Intel QSV | AMD AMF | 软件回退 |
|---|---|---|---|---|
| H.264 | h264_nvenc | h264_qsv | h264_amf | libx264 |
| H.265 | hevc_nvenc | hevc_qsv | hevc_amf | libx265 |
| AV1 | av1_nvenc | av1_qsv | av1_amf | libsvtav1 / libaom-av1 |
| VP9 | （无 nvenc） | vp9_qsv | （无 amf） | libvpx-vp9 |

注：ffmpeg 9 起 VP9 的 NVENC 编码器已移除；AV1 的软件编码器在本构建中需另行确认
（`libaom-av1` / `libsvtav1` 是否存在），若缺失则 AV1 仅在硬件可用时可选。

## 各编码器的质量控制参数映射（CRF 语义不通用）

| 编码器 | 质量参数 |
|---|---|
| libx264 / libx265 | `-crf N` |
| libvpx-vp9 | `-crf N -b:v 0` |
| h264_nvenc / hevc_nvenc | `-rc vbr -cq N -b:v 0` |
| h264_qsv / hevc_qsv | `-global_quality N` |
| h264_amf / hevc_amf | `-qp N` |

CRF 数值域在软件编码器是 0-51（越小质量越高），硬件编码器的 -cq/-qp 语义接近但不完全相同。
UI 上统一用"质量"滑杆 0-51，内部按编码器映射到对应参数。

## 运行时兜底

即使探测通过，实际转换仍可能失败（驱动更新、GPU 被占用、显存不足等）。
因此需要：硬件编码失败 -> 自动用软件编码重试 -> 将该编码器缓存标记为不可用。
