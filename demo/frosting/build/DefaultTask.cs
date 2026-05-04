using Build.Tasks;
using Cake.Frosting;

namespace Build
{
    [TaskName("Default")]
    [IsDependentOn(typeof(ArgumentValidationTask))]
    [IsDependentOn(typeof(DefaultResultFallbackTask))]
    [IsDependentOn(typeof(TimeoutFiresTask))]
    public sealed class DefaultTask : FrostingTask
    {
    }
}
