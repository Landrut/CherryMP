using System;

namespace CherryMP.GUI.DirectXHook.Hook
{
    public interface IDXHook: IDisposable
    {
        void Hook();

        void Cleanup();
    }
}
