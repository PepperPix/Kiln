namespace Kiln.Services;

public interface IImageOptimizer
{
    bool CanOptimize(string extension);

    ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings);
}
