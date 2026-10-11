using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
