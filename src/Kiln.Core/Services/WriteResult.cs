namespace Kiln.Services;

public enum WriteResult
{
    Written,
    SkippedAdopted,
    Conflict,
}
