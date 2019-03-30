using System.Collections.Generic;

namespace CherryMP.GUI.DirectXHook.Hook.Common
{
    internal interface IOverlay: IOverlayElement
    {
        List<IOverlayElement> Elements { get; set; }
    }
}
