using FluentValidation;
using JetBrains.Annotations;

namespace VirtualSteward;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class VirtualStewardConfigurationValidator : AbstractValidator<VirtualStewardConfiguration>
{
    public VirtualStewardConfigurationValidator( )
    {
    }
}
