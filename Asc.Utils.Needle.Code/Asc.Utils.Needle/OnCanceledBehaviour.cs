namespace Asc.Utils.Needle;

[Flags]
internal enum OnCanceledBehaviour
{
    InvokeCanceledEvent,
    ThrowOperationCanceledException
}
