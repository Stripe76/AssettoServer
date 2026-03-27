using FluentValidation;
using JetBrains.Annotations;

namespace VSReplayPlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class VSReplayConfigurationValidator : AbstractValidator<VSReplayConfiguration>
{
    public VSReplayConfigurationValidator( )
    {
    }
}
