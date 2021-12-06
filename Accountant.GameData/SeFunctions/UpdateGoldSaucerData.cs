﻿using System;
using Dalamud.Game;

namespace Accountant.SeFunctions;


public delegate void UpdateGoldSaucerDelegate(IntPtr unk, IntPtr packetData);

public sealed class UpdateGoldSaucerData : SeFunctionBase<UpdateGoldSaucerDelegate>
{
    public UpdateGoldSaucerData(SigScanner sigScanner)
        : base(sigScanner, "?? 89 ?? ?? ?? 57 48 83 ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 8B ?? E8 ?? ?? ?? ?? 48 8B ?? 4C ?? ?? ?? ?? ?? ?? ?? ?? ?? 48 8B ?? BA B0")
    { }
}
