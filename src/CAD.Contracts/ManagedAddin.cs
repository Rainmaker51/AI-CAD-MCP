using System;
using System.Collections.Generic;

namespace AICad.Contracts;

// Optional host contract: palette actions route through Manager instead of assuming globally NETLOADed commands.
public interface ICadManagedAddin
{
    IReadOnlyList<CadRibbonPin> ManagedCommands { get; }
    void InitializeManaged(Action<string> dispatch);
}
