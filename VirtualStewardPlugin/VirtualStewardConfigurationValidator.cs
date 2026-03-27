using FluentValidation;
using JetBrains.Annotations;

namespace VirtualStewardPlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class VirtualStewardConfigurationValidator : AbstractValidator<VirtualStewardConfiguration>
{
    public VirtualStewardConfigurationValidator( )
    {
    }
}
