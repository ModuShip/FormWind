#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    // .NET Framework'te yok; derleyici modül başlatıcısını adından tanır.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
